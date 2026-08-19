using MediatR;
using PortalIUPA.Application.Common;
using PortalIUPA.Application.DTOs;
using PortalIUPA.Application.Services;
using PortalIUPA.Domain.DomainServices;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Application.UseCases.Licencias;

/// <summary>
/// Aprobar o rechazar el nivel pendiente de una solicitud. La decisión es inmutable (auditoría) y
/// avanza el workflow: rechazo corta la cadena, aprobación habilita el siguiente nivel.
/// </summary>
public sealed record DecidirSolicitudCommand(Guid SolicitudId, Guid AprobadorId, bool Aprobado, string? Comentario)
    : IRequest<SolicitudLicenciaDto>;

public sealed class DecidirSolicitudCommandHandler : IRequestHandler<DecidirSolicitudCommand, SolicitudLicenciaDto>
{
    private readonly ISolicitudLicenciaRepository _solicitudes;
    private readonly ITipoLicenciaRepository _tipos;
    private readonly IAprobacionRepository _aprobaciones;
    private readonly IEmpleadoRepository _empleados;
    private readonly IAprobadorResolver _resolver;
    private readonly NotificadorLicencias _notificador;

    public DecidirSolicitudCommandHandler(ISolicitudLicenciaRepository solicitudes, ITipoLicenciaRepository tipos,
        IAprobacionRepository aprobaciones, IEmpleadoRepository empleados, IAprobadorResolver resolver,
        NotificadorLicencias notificador)
    {
        _solicitudes = solicitudes;
        _tipos = tipos;
        _aprobaciones = aprobaciones;
        _empleados = empleados;
        _resolver = resolver;
        _notificador = notificador;
    }

    public async Task<SolicitudLicenciaDto> Handle(DecidirSolicitudCommand request, CancellationToken ct)
    {
        var solicitud = await _solicitudes.GetByIdAsync(request.SolicitudId, ct)
            ?? throw new EntidadNoEncontradaException("La solicitud no existe.");
        var tipo = await _tipos.GetByIdAsync(solicitud.TipoLicenciaId, ct)
            ?? throw new EntidadNoEncontradaException("El tipo de licencia no existe.");
        var decisiones = await _aprobaciones.GetBySolicitudAsync(solicitud.Id, ct);

        if (solicitud.Estado != EstadoSolicitud.EnEspera)
            throw new ReglaDeNegocioException("La solicitud ya fue resuelta o cancelada.");

        var nivel = WorkflowAprobacion.SiguienteNivelPendiente(tipo, decisiones)
            ?? throw new ReglaDeNegocioException("La solicitud no tiene niveles pendientes de aprobación.");

        var aprobador = await _empleados.GetByIdAsync(request.AprobadorId, ct)
            ?? throw new EntidadNoEncontradaException("El aprobador no existe.");

        if (!await _resolver.PuedeActuarAsync(solicitud, aprobador, ct))
            throw new AccesoDenegadoException("No está habilitado para aprobar esta solicitud en el nivel actual.");

        var resultado = request.Aprobado ? ResultadoAprobacion.Aprobado : ResultadoAprobacion.Rechazado;
        var aprobacion = new Aprobacion(solicitud.Id, nivel.Id, aprobador.Id, resultado, request.Comentario);
        await _aprobaciones.AddAsync(aprobacion, ct);

        var nuevasDecisiones = decisiones.Concat(new[] { aprobacion });
        var nuevoEstado = WorkflowAprobacion.CalcularEstado(tipo, nuevasDecisiones);
        solicitud.ActualizarEstado(nuevoEstado);
        await _solicitudes.UpdateAsync(solicitud, ct);

        var empleado = await _empleados.GetByIdAsync(solicitud.EmpleadoId, ct);
        if (empleado is not null)
        {
            var accion = request.Aprobado ? "aprobada" : "rechazada";
            await _notificador.AvisarEmpleadoAsync(empleado,
                $"Licencia {accion}",
                $"Su solicitud de <b>{tipo.Nombre}</b> del {solicitud.FechaInicio:dd/MM/yyyy} al " +
                $"{solicitud.FechaFin:dd/MM/yyyy} fue <b>{accion}</b> por {aprobador.NombreCompleto} " +
                $"(nivel: {nivel.RolRequerido}).", "/licencias", ct);
        }

        if (nuevoEstado == EstadoSolicitud.EnEspera)
        {
            var siguiente = WorkflowAprobacion.SiguienteNivelPendiente(tipo, nuevasDecisiones);
            if (siguiente is not null)
            {
                var aprobadores = await _resolver.ResolverAprobadoresAsync(solicitud, siguiente, ct);
                await _notificador.AvisarAprobadoresAsync(aprobadores.Select(a => (
                    Aprobador: a,
                    TipoLicencia: tipo.Nombre,
                    Solicitante: empleado?.NombreCompleto ?? "?",
                    Inicio: solicitud.FechaInicio,
                    Fin: solicitud.FechaFin)), ct);
            }
        }

        return MapeadorSolicitudes.Mapear(solicitud,
            new Dictionary<Guid, TipoLicencia> { [tipo.Id] = tipo },
            (await _empleados.GetAllAsync(ct)).ToDictionary(e => e.Id),
            nuevasDecisiones);
    }
}

/// <summary>El empleado dueño de la solicitud puede cancelarla mientras esté en espera.</summary>
public sealed record CancelarSolicitudCommand(Guid SolicitudId, Guid EmpleadoId) : IRequest<Unit>;

public sealed class CancelarSolicitudCommandHandler : IRequestHandler<CancelarSolicitudCommand, Unit>
{
    private readonly ISolicitudLicenciaRepository _solicitudes;

    public CancelarSolicitudCommandHandler(ISolicitudLicenciaRepository solicitudes) => _solicitudes = solicitudes;

    public async Task<Unit> Handle(CancelarSolicitudCommand request, CancellationToken ct)
    {
        var solicitud = await _solicitudes.GetByIdAsync(request.SolicitudId, ct)
            ?? throw new EntidadNoEncontradaException("La solicitud no existe.");

        if (solicitud.EmpleadoId != request.EmpleadoId)
            throw new AccesoDenegadoException("Solo el solicitante puede cancelar su propia licencia.");

        solicitud.Cancelar();
        await _solicitudes.UpdateAsync(solicitud, ct);

        return Unit.Value;
    }
}