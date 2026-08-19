using MediatR;
using PortalIUPA.Application.Common;
using PortalIUPA.Application.DTOs;
using PortalIUPA.Application.Services;
using PortalIUPA.Domain.DomainServices;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Application.UseCases.Licencias;

public sealed record GetMisSolicitudesQuery(Guid EmpleadoId) : IRequest<IReadOnlyList<SolicitudLicenciaDto>>;

public sealed class GetMisSolicitudesQueryHandler : IRequestHandler<GetMisSolicitudesQuery,
    IReadOnlyList<SolicitudLicenciaDto>>
{
    private readonly ISolicitudLicenciaRepository _solicitudes;
    private readonly ITipoLicenciaRepository _tipos;
    private readonly IEmpleadoRepository _empleados;
    private readonly IAprobacionRepository _aprobaciones;

    public GetMisSolicitudesQueryHandler(ISolicitudLicenciaRepository solicitudes, ITipoLicenciaRepository tipos,
        IEmpleadoRepository empleados, IAprobacionRepository aprobaciones)
    {
        _solicitudes = solicitudes;
        _tipos = tipos;
        _empleados = empleados;
        _aprobaciones = aprobaciones;
    }

    public async Task<IReadOnlyList<SolicitudLicenciaDto>> Handle(GetMisSolicitudesQuery request,
        CancellationToken ct)
    {
        var solicitudes = await _solicitudes.GetByEmpleadoAsync(request.EmpleadoId, ct);
        var tipos = (await _tipos.GetAllAsync(ct)).ToDictionary(t => t.Id);
        var empleados = (await _empleados.GetAllAsync(ct)).ToDictionary(e => e.Id);

        var resultado = new List<SolicitudLicenciaDto>();
        foreach (var solicitud in solicitudes.OrderByDescending(s => s.FechaSolicitud))
        {
            var aprobaciones = await _aprobaciones.GetBySolicitudAsync(solicitud.Id, ct);
            resultado.Add(MapeadorSolicitudes.Mapear(solicitud, tipos, empleados, aprobaciones));
        }

        return resultado;
    }
}

public sealed record GetSolicitudDetalleQuery(Guid SolicitudId, Guid UsuarioId) : IRequest<SolicitudDetalleDto>;

public sealed class GetSolicitudDetalleQueryHandler : IRequestHandler<GetSolicitudDetalleQuery, SolicitudDetalleDto>
{
    private readonly ISolicitudLicenciaRepository _solicitudes;
    private readonly ITipoLicenciaRepository _tipos;
    private readonly IEmpleadoRepository _empleados;
    private readonly IAprobacionRepository _aprobaciones;
    private readonly IAprobadorResolver _resolver;

    public GetSolicitudDetalleQueryHandler(ISolicitudLicenciaRepository solicitudes, ITipoLicenciaRepository tipos,
        IEmpleadoRepository empleados, IAprobacionRepository aprobaciones, IAprobadorResolver resolver)
    {
        _solicitudes = solicitudes;
        _tipos = tipos;
        _empleados = empleados;
        _aprobaciones = aprobaciones;
        _resolver = resolver;
    }

    public async Task<SolicitudDetalleDto> Handle(GetSolicitudDetalleQuery request, CancellationToken ct)
    {
        var solicitud = await _solicitudes.GetByIdAsync(request.SolicitudId, ct)
            ?? throw new EntidadNoEncontradaException("La solicitud no existe.");
        var tipos = (await _tipos.GetAllAsync(ct)).ToDictionary(t => t.Id);
        var empleados = (await _empleados.GetAllAsync(ct)).ToDictionary(e => e.Id);
        var aprobaciones = await _aprobaciones.GetBySolicitudAsync(solicitud.Id, ct);

        var dto = MapeadorSolicitudes.Mapear(solicitud, tipos, empleados, aprobaciones);
        var tipo = tipos.GetValueOrDefault(solicitud.TipoLicenciaId);

        NivelAprobacionDto? nivelPendiente = null;
        var puedeAprobar = false;
        string? razonBloqueo = null;

        if (solicitud.Estado == Domain.Enums.EstadoSolicitud.EnEspera && tipo is not null)
        {
            var pendiente = WorkflowAprobacion.SiguienteNivelPendiente(tipo, aprobaciones);
            if (pendiente is not null)
            {
                nivelPendiente = new NivelAprobacionDto(pendiente.Id, pendiente.Orden, pendiente.RolRequerido);
                var usuario = empleados.GetValueOrDefault(request.UsuarioId);
                puedeAprobar = usuario is not null && await _resolver.PuedeActuarAsync(solicitud, usuario, ct);
                if (!puedeAprobar && usuario is not null)
                {
                    var aprobadores = await _resolver.ResolverAprobadoresAsync(solicitud, pendiente, ct);
                    razonBloqueo = aprobadores.Count == 0
                        ? "No hay aprobadores disponibles para el nivel actual (falta asignar responsable o rol)."
                        : null;
                }
            }
        }

        return new SolicitudDetalleDto(dto,
            tipo?.NivelesOrdenados.Select(n => new NivelAprobacionDto(n.Id, n.Orden, n.RolRequerido)).ToList()
            ?? new List<NivelAprobacionDto>(),
            nivelPendiente, puedeAprobar, razonBloqueo);
    }
}

/// <summary>Solicitudes en espera donde el usuario es el aprobador del nivel actual (bandeja del responsable).</summary>
public sealed record GetPendientesDeMiAprobacionQuery(Guid AprobadorId)
    : IRequest<IReadOnlyList<SolicitudLicenciaDto>>;

public sealed class GetPendientesDeMiAprobacionQueryHandler : IRequestHandler<GetPendientesDeMiAprobacionQuery,
    IReadOnlyList<SolicitudLicenciaDto>>
{
    private readonly ISolicitudLicenciaRepository _solicitudes;
    private readonly ITipoLicenciaRepository _tipos;
    private readonly IEmpleadoRepository _empleados;
    private readonly IAprobacionRepository _aprobaciones;
    private readonly IAprobadorResolver _resolver;

    public GetPendientesDeMiAprobacionQueryHandler(ISolicitudLicenciaRepository solicitudes,
        ITipoLicenciaRepository tipos, IEmpleadoRepository empleados, IAprobacionRepository aprobaciones,
        IAprobadorResolver resolver)
    {
        _solicitudes = solicitudes;
        _tipos = tipos;
        _empleados = empleados;
        _aprobaciones = aprobaciones;
        _resolver = resolver;
    }

    public async Task<IReadOnlyList<SolicitudLicenciaDto>> Handle(GetPendientesDeMiAprobacionQuery request,
        CancellationToken ct)
    {
        var yo = await _empleados.GetByIdAsync(request.AprobadorId, ct);
        if (yo is null)
            throw new EntidadNoEncontradaException("El empleado no existe.");

        var enEspera = await _solicitudes.GetEnEsperaAsync(ct);
        var tipos = (await _tipos.GetAllAsync(ct)).ToDictionary(t => t.Id);
        var empleados = (await _empleados.GetAllAsync(ct)).ToDictionary(e => e.Id);

        var resultado = new List<SolicitudLicenciaDto>();
        foreach (var solicitud in enEspera)
        {
            if (!await _resolver.PuedeActuarAsync(solicitud, yo, ct))
                continue;
            var aprobaciones = await _aprobaciones.GetBySolicitudAsync(solicitud.Id, ct);
            resultado.Add(MapeadorSolicitudes.Mapear(solicitud, tipos, empleados, aprobaciones));
        }

        return resultado.OrderByDescending(s => s.FechaSolicitud).ToList();
    }
}

/// <summary>Bandeja global de pendientes (RRHH/Admin).</summary>
public sealed record GetPendientesGlobalesQuery : IRequest<IReadOnlyList<SolicitudLicenciaDto>>;

public sealed class GetPendientesGlobalesQueryHandler : IRequestHandler<GetPendientesGlobalesQuery,
    IReadOnlyList<SolicitudLicenciaDto>>
{
    private readonly ISolicitudLicenciaRepository _solicitudes;
    private readonly ITipoLicenciaRepository _tipos;
    private readonly IEmpleadoRepository _empleados;
    private readonly IAprobacionRepository _aprobaciones;

    public GetPendientesGlobalesQueryHandler(ISolicitudLicenciaRepository solicitudes,
        ITipoLicenciaRepository tipos, IEmpleadoRepository empleados, IAprobacionRepository aprobaciones)
    {
        _solicitudes = solicitudes;
        _tipos = tipos;
        _empleados = empleados;
        _aprobaciones = aprobaciones;
    }

    public async Task<IReadOnlyList<SolicitudLicenciaDto>> Handle(GetPendientesGlobalesQuery request,
        CancellationToken ct)
    {
        var enEspera = await _solicitudes.GetEnEsperaAsync(ct);
        var tipos = (await _tipos.GetAllAsync(ct)).ToDictionary(t => t.Id);
        var empleados = (await _empleados.GetAllAsync(ct)).ToDictionary(e => e.Id);

        var resultado = new List<SolicitudLicenciaDto>();
        foreach (var solicitud in enEspera)
        {
            var aprobaciones = await _aprobaciones.GetBySolicitudAsync(solicitud.Id, ct);
            resultado.Add(MapeadorSolicitudes.Mapear(solicitud, tipos, empleados, aprobaciones));
        }

        return resultado.OrderByDescending(s => s.FechaSolicitud).ToList();
    }
}