using FluentValidation;
using MediatR;
using PortalIUPA.Application.Common;
using PortalIUPA.Application.DTOs;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Application.UseCases.Admin;

/// <summary>Asigna un responsable a un empleado (finaliza la relación vigente anterior del empleado).</summary>
public sealed record AsignarResponsableCommand(Guid ResponsableId, Guid EmpleadoId, bool AutorizaMarcas)
    : IRequest<RelacionACargoDto>;

public sealed class AsignarResponsableCommandHandler : IRequestHandler<AsignarResponsableCommand, RelacionACargoDto>
{
    private readonly IRelacionACargoRepository _relaciones;
    private readonly IEmpleadoRepository _empleados;

    public AsignarResponsableCommandHandler(IRelacionACargoRepository relaciones, IEmpleadoRepository empleados)
    {
        _relaciones = relaciones;
        _empleados = empleados;
    }

    public async Task<RelacionACargoDto> Handle(AsignarResponsableCommand request, CancellationToken ct)
    {
        if (request.ResponsableId == request.EmpleadoId)
            throw new ReglaDeNegocioException("Un empleado no puede ser responsable de sí mismo.");

        var responsable = await _empleados.GetByIdAsync(request.ResponsableId, ct)
            ?? throw new EntidadNoEncontradaException("El responsable no existe.");
        var empleado = await _empleados.GetByIdAsync(request.EmpleadoId, ct)
            ?? throw new EntidadNoEncontradaException("El empleado no existe.");

        var vigente = await _relaciones.GetVigenteDeEmpleadoAsync(request.EmpleadoId, ct);
        if (vigente is not null && vigente.ResponsableId == request.ResponsableId)
        {
            vigente.CambiarAutorizacionDeMarcas(request.AutorizaMarcas);
            await _relaciones.UpdateAsync(vigente, ct);
            return new RelacionACargoDto(vigente.Id, vigente.ResponsableId, responsable.NombreCompleto,
                vigente.EmpleadoId, empleado.NombreCompleto, vigente.AutorizaMarcas, vigente.FechaDesde,
                vigente.FechaHasta);
        }

        if (vigente is not null)
        {
            vigente.Finalizar(DateOnly.FromDateTime(DateTime.Today));
            await _relaciones.UpdateAsync(vigente, ct);
        }

        var nueva = new RelacionACargo(request.ResponsableId, request.EmpleadoId, request.AutorizaMarcas,
            DateOnly.FromDateTime(DateTime.Today));
        await _relaciones.AddAsync(nueva, ct);

        return new RelacionACargoDto(nueva.Id, nueva.ResponsableId, responsable.NombreCompleto, nueva.EmpleadoId,
            empleado.NombreCompleto, nueva.AutorizaMarcas, nueva.FechaDesde, nueva.FechaHasta);
    }
}

/// <summary>Finaliza (sin borrar) la relación "a cargo" para conservar el historial de supervisión.</summary>
public sealed record QuitarResponsableCommand(Guid RelacionId) : IRequest<Unit>;

public sealed class QuitarResponsableCommandHandler : IRequestHandler<QuitarResponsableCommand, Unit>
{
    private readonly IRelacionACargoRepository _relaciones;

    public QuitarResponsableCommandHandler(IRelacionACargoRepository relaciones) => _relaciones = relaciones;

    public async Task<Unit> Handle(QuitarResponsableCommand request, CancellationToken ct)
    {
        var relaciones = await _relaciones.GetVigentesAsync(ct);
        var relacion = relaciones.FirstOrDefault(r => r.Id == request.RelacionId)
            ?? throw new EntidadNoEncontradaException("La relación no existe.");

        relacion.Finalizar(DateOnly.FromDateTime(DateTime.Today));
        await _relaciones.UpdateAsync(relacion, ct);

        return Unit.Value;
    }
}

public sealed record GetACargoQuery(Guid ResponsableId) : IRequest<IReadOnlyList<RelacionACargoDto>>;

public sealed class GetACargoQueryHandler : IRequestHandler<GetACargoQuery, IReadOnlyList<RelacionACargoDto>>
{
    private readonly IRelacionACargoRepository _relaciones;
    private readonly IEmpleadoRepository _empleados;

    public GetACargoQueryHandler(IRelacionACargoRepository relaciones, IEmpleadoRepository empleados)
    {
        _relaciones = relaciones;
        _empleados = empleados;
    }

    public async Task<IReadOnlyList<RelacionACargoDto>> Handle(GetACargoQuery request, CancellationToken ct)
    {
        var relaciones = await _relaciones.GetVigentesDeResponsableAsync(request.ResponsableId, ct);
        var empleados = (await _empleados.GetAllAsync(ct)).ToDictionary(e => e.Id);

        return relaciones
            .Select(r => new RelacionACargoDto(r.Id, r.ResponsableId,
                empleados.GetValueOrDefault(r.ResponsableId)?.NombreCompleto ?? "?",
                r.EmpleadoId, empleados.GetValueOrDefault(r.EmpleadoId)?.NombreCompleto ?? "?",
                r.AutorizaMarcas, r.FechaDesde, r.FechaHasta))
            .ToList();
    }
}

/// <summary>Organigrama: árbol de áreas con sus empleados.</summary>
public sealed record GetOrganigramaQuery : IRequest<IReadOnlyList<OrganigramaNodoDto>>;

public sealed class GetOrganigramaQueryHandler : IRequestHandler<GetOrganigramaQuery, IReadOnlyList<OrganigramaNodoDto>>
{
    private readonly IAreaRepository _areas;
    private readonly IEmpleadoRepository _empleados;

    public GetOrganigramaQueryHandler(IAreaRepository areas, IEmpleadoRepository empleados)
    {
        _areas = areas;
        _empleados = empleados;
    }

    public async Task<IReadOnlyList<OrganigramaNodoDto>> Handle(GetOrganigramaQuery request, CancellationToken ct)
    {
        var areas = await _areas.GetAllAsync(ct);
        var empleados = await _empleados.GetActivosAsync(ct);
        var areasPorPadre = areas.Where(a => a.AreaPadreId.HasValue)
            .GroupBy(a => a.AreaPadreId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        OrganigramaNodoDto Construir(Area area)
        {
            var hijas = areasPorPadre.GetValueOrDefault(area.Id) ?? new List<Area>();
            var empleadosDelArea = empleados.Where(e => e.AreaId == area.Id)
                .OrderBy(e => e.Apellido)
                .Select(e => new EmpleadoDto(e.Id, e.Legajo, e.Nombre, e.Apellido, e.Dni, e.Cuil, e.Correo.Valor,
                    e.AreaId, area.Nombre, e.Activo, e.Roles.ToList()))
                .ToList();
            return new OrganigramaNodoDto(area.Id, area.Nombre, area.Codigo,
                hijas.Select(Construir).ToList(), empleadosDelArea);
        }

        var raices = areas.Where(a => !a.AreaPadreId.HasValue).ToList();
        return raices.Select(Construir).ToList();
    }
}