using FluentValidation;
using MediatR;
using PortalIUPA.Application.Common;
using PortalIUPA.Application.DTOs;
using PortalIUPA.Application.Services;
using PortalIUPA.Domain.DomainServices;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.Ports;
using PortalIUPA.Domain.ValueObjects;

namespace PortalIUPA.Application.UseCases.Licencias;

/// <summary>
/// Crea una solicitud de licencia validando: tipo activo, fechas, límites mensual/anual (regla de dominio),
/// no solapamiento con otras solicitudes y adjunto obligatorio si el tipo lo requiere.
/// </summary>
public sealed record SolicitarLicenciaCommand(Guid EmpleadoId, Guid TipoLicenciaId, DateOnly FechaInicio,
    DateOnly FechaFin, string? Asunto, string? Motivo, Guid? AdjuntoId) : IRequest<NuevaSolicitudResultado>;

public sealed class SolicitarLicenciaCommandValidator : AbstractValidator<SolicitarLicenciaCommand>
{
    public SolicitarLicenciaCommandValidator()
    {
        RuleFor(c => c.TipoLicenciaId).NotEmpty().WithMessage("El tipo de licencia es obligatorio.");
        RuleFor(c => c.FechaFin).GreaterThanOrEqualTo(c => c.FechaInicio)
            .WithMessage("La fecha de fin no puede ser anterior a la de inicio.");
    }
}

public sealed class SolicitarLicenciaCommandHandler : IRequestHandler<SolicitarLicenciaCommand, NuevaSolicitudResultado>
{
    private readonly ITipoLicenciaRepository _tipos;
    private readonly ISolicitudLicenciaRepository _solicitudes;
    private readonly IEmpleadoRepository _empleados;
    private readonly IAdjuntoRepository _adjuntos;
    private readonly IAprobadorResolver _resolver;
    private readonly NotificadorLicencias _notificador;

    public SolicitarLicenciaCommandHandler(ITipoLicenciaRepository tipos, ISolicitudLicenciaRepository solicitudes,
        IEmpleadoRepository empleados, IAdjuntoRepository adjuntos, IAprobadorResolver resolver,
        NotificadorLicencias notificador)
    {
        _tipos = tipos;
        _solicitudes = solicitudes;
        _empleados = empleados;
        _adjuntos = adjuntos;
        _resolver = resolver;
        _notificador = notificador;
    }

    public async Task<NuevaSolicitudResultado> Handle(SolicitarLicenciaCommand request, CancellationToken ct)
    {
        var tipo = await _tipos.GetByIdAsync(request.TipoLicenciaId, ct)
            ?? throw new EntidadNoEncontradaException("El tipo de licencia no existe.");
        if (!tipo.Activo)
            throw new ReglaDeNegocioException("El tipo de licencia seleccionado no está activo.");

        var empleado = await _empleados.GetByIdAsync(request.EmpleadoId, ct)
            ?? throw new EntidadNoEncontradaException("El empleado no existe.");

        RangoFechas rango;
        try
        {
            rango = new RangoFechas(request.FechaInicio, request.FechaFin);
        }
        catch (ArgumentException ex)
        {
            throw new ReglaDeNegocioException(ex.Message);
        }

        if (tipo.RequiereAdjunto && request.AdjuntoId is null)
            throw new ReglaDeNegocioException($"El tipo de licencia '{tipo.Nombre}' requiere adjuntar un certificado.");

        var consumo = await _solicitudes.GetConsumoAsync(request.EmpleadoId, tipo.Id, request.FechaInicio.Year,
            request.FechaInicio.Month, ct);
        var validacion = ValidadorLimitesLicencia.Validar(tipo,
            rango.DiasDentroDelMes(request.FechaInicio.Year, request.FechaInicio.Month),
            rango.DiasDentroDelAnio(request.FechaInicio.Year),
            consumo.DiasMes, consumo.DiasAnio);
        if (!validacion.EsValido)
            throw new ReglaDeNegocioException(validacion.Error!);

        var solapadas = await _solicitudes.GetSolapadasAsync(request.EmpleadoId, tipo.Id, rango, ct);
        if (solapadas.Count > 0)
            throw new ReglaDeNegocioException(
                $"Ya tiene una solicitud de '{tipo.Nombre}' del {solapadas[0].FechaInicio:dd/MM/yyyy} " +
                $"al {solapadas[0].FechaFin:dd/MM/yyyy} que se superpone con el período solicitado.");

        var solicitud = new SolicitudLicencia(request.EmpleadoId, tipo.Id, rango, request.Asunto, request.Motivo,
            request.AdjuntoId);
        var estadoInicial = WorkflowAprobacion.CalcularEstado(tipo, Array.Empty<Aprobacion>());
        solicitud.ActualizarEstado(estadoInicial);

        await _solicitudes.AddAsync(solicitud, ct);

        if (request.AdjuntoId is not null)
        {
            var adjunto = await _adjuntos.GetByIdAsync(request.AdjuntoId.Value, ct)
                ?? throw new ReglaDeNegocioException("El adjunto indicado no existe.");
            if (adjunto.EmpleadoId != request.EmpleadoId)
                throw new ReglaDeNegocioException("El adjunto pertenece a otro empleado.");
            adjunto.VincularAEntidad("solicitud", solicitud.Id);
            await _adjuntos.ActualizarAsync(adjunto, ct);
        }

        var nivelPendiente = await _resolver.NivelPendienteAsync(solicitud, ct);
        if (nivelPendiente is not null)
        {
            var aprobadores = await _resolver.ResolverAprobadoresAsync(solicitud, nivelPendiente, ct);
            await _notificador.AvisarAprobadoresAsync(aprobadores.Select(a => (
                Aprobador: a,
                TipoLicencia: tipo.Nombre,
                Solicitante: empleado.NombreCompleto,
                Inicio: solicitud.FechaInicio,
                Fin: solicitud.FechaFin)), ct);
        }

        await _notificador.AvisarEmpleadoAsync(empleado, "Solicitud de licencia registrada",
            $"Su solicitud de <b>{tipo.Nombre}</b> del {solicitud.FechaInicio:dd/MM/yyyy} al " +
            $"{solicitud.FechaFin:dd/MM/yyyy} quedó en estado <b>{solicitud.Estado}</b>.", "/licencias", ct);

        return new NuevaSolicitudResultado(solicitud.Id, solicitud.Estado);
    }
}