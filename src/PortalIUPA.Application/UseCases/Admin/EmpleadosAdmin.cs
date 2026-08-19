using FluentValidation;
using MediatR;
using PortalIUPA.Application.Common;
using PortalIUPA.Application.DTOs;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.Ports;
using PortalIUPA.Domain.ValueObjects;

namespace PortalIUPA.Application.UseCases.Admin;

/// <summary>Filtrado y ordenamiento compartido entre la grilla paginada y la exportación.</summary>
internal static class EmpleadosFiltro
{
    public static List<EmpleadoDto> FiltrarYOrdenar(IReadOnlyList<Empleado> empleados,
        IReadOnlyDictionary<Guid, Area> areas, string? texto, string? orden, bool descendente)
    {
        IEnumerable<Empleado> filtrados = empleados;
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim();
            filtrados = empleados.Where(e =>
                e.Nombre.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                e.Apellido.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                e.Correo.Valor.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                e.Legajo.ToString() == t ||
                (e.Dni is not null && e.Dni.Contains(t)));
        }

        var dto = filtrados.Select(MapearEmpleado(areas)).ToList();

        var clave = (orden ?? "").Trim().ToLowerInvariant();
        return clave switch
        {
            "legajo" => Aplicar(dto, x => x.Legajo, descendente),
            "correo" => Aplicar(dto, x => x.Correo, descendente),
            "area" => Aplicar(dto, x => x.AreaNombre ?? "", descendente),
            "activo" => Aplicar(dto, x => x.Activo, descendente),
            "nombre" => descendente
                ? dto.OrderByDescending(x => x.Apellido).ThenByDescending(x => x.Nombre).ToList()
                : dto.OrderBy(x => x.Apellido).ThenBy(x => x.Nombre).ToList(),
            _ => descendente
                ? dto.OrderByDescending(x => x.Apellido).ThenByDescending(x => x.Nombre).ToList()
                : dto.OrderBy(x => x.Apellido).ThenBy(x => x.Nombre).ToList()
        };
    }

    private static List<T> Aplicar<T, K>(IEnumerable<T> items, Func<T, K> clave, bool descendente) =>
        descendente ? items.OrderByDescending(clave).ToList() : items.OrderBy(clave).ToList();

    private static Func<Empleado, EmpleadoDto> MapearEmpleado(IReadOnlyDictionary<Guid, Area> areas) =>
        e => new EmpleadoDto(e.Id, e.Legajo, e.Nombre, e.Apellido, e.Dni, e.Cuil, e.Correo.Valor, e.AreaId,
            e.AreaId is { } id && areas.TryGetValue(id, out var area) ? area.Nombre : null, e.Activo,
            e.Roles.ToList());
}

public sealed record GetEmpleadosQuery(string? Texto = null, int Pagina = 1, int Tamano = 20,
    string? Orden = "Apellido", bool Descendente = false) : IRequest<PaginaEmpleadosDto>;

public sealed class GetEmpleadosQueryHandler : IRequestHandler<GetEmpleadosQuery, PaginaEmpleadosDto>
{
    private readonly IEmpleadoRepository _empleados;
    private readonly IAreaRepository _areas;

    public GetEmpleadosQueryHandler(IEmpleadoRepository empleados, IAreaRepository areas)
    {
        _empleados = empleados;
        _areas = areas;
    }

    public async Task<PaginaEmpleadosDto> Handle(GetEmpleadosQuery request, CancellationToken ct)
    {
        var empleados = await _empleados.GetAllAsync(ct);
        var areas = (await _areas.GetAllAsync(ct)).ToDictionary(a => a.Id);

        var dto = EmpleadosFiltro.FiltrarYOrdenar(empleados, areas, request.Texto, request.Orden,
            request.Descendente);

        var pagina = Math.Max(request.Pagina, 1);
        var tamano = request.Tamano is <= 0 or > 200 ? 20 : request.Tamano;
        var total = dto.Count;

        return new PaginaEmpleadosDto(dto.Skip((pagina - 1) * tamano).Take(tamano).ToList(),
            total, pagina, tamano);
    }
}

public sealed record GetEmpleadoDetalleQuery(Guid Id) : IRequest<EmpleadoDetalleDto>;

public sealed class GetEmpleadoDetalleQueryHandler : IRequestHandler<GetEmpleadoDetalleQuery, EmpleadoDetalleDto>
{
    private readonly IEmpleadoRepository _empleados;
    private readonly IAreaRepository _areas;
    private readonly IRelacionACargoRepository _relaciones;

    public GetEmpleadoDetalleQueryHandler(IEmpleadoRepository empleados, IAreaRepository areas,
        IRelacionACargoRepository relaciones)
    {
        _empleados = empleados;
        _areas = areas;
        _relaciones = relaciones;
    }

    public async Task<EmpleadoDetalleDto> Handle(GetEmpleadoDetalleQuery request, CancellationToken ct)
    {
        var empleado = await _empleados.GetByIdAsync(request.Id, ct)
            ?? throw new EntidadNoEncontradaException("El empleado no existe.");
        var areas = (await _areas.GetAllAsync(ct)).ToDictionary(a => a.Id);

        var relacion = await _relaciones.GetVigenteDeEmpleadoAsync(empleado.Id, ct);
        var responsable = relacion is not null
            ? await _empleados.GetByIdAsync(relacion.ResponsableId, ct)
            : null;
        var aCargo = await _relaciones.GetVigentesDeResponsableAsync(empleado.Id, ct);
        var aCargoEmpleados = new List<EmpleadoDto>();
        foreach (var r in aCargo)
        {
            var e = await _empleados.GetByIdAsync(r.EmpleadoId, ct);
            if (e is not null)
                aCargoEmpleados.Add(new EmpleadoDto(e.Id, e.Legajo, e.Nombre, e.Apellido, e.Dni, e.Cuil,
                    e.Correo.Valor, e.AreaId, e.AreaId is { } id && areas.TryGetValue(id, out var nombreArea) ? nombreArea.Nombre : null,
                    e.Activo, e.Roles.ToList()));
        }

        var dto = new EmpleadoDto(empleado.Id, empleado.Legajo, empleado.Nombre, empleado.Apellido, empleado.Dni,
            empleado.Cuil, empleado.Correo.Valor, empleado.AreaId,
            empleado.AreaId is { } aid && areas.TryGetValue(aid, out var nombreArea2) ? nombreArea2.Nombre : null, empleado.Activo,
            empleado.Roles.ToList());

        return new EmpleadoDetalleDto(dto, responsable?.Id, responsable?.NombreCompleto, relacion?.AutorizaMarcas ?? false,
            aCargoEmpleados);
    }
}

public sealed record CreateEmpleadoCommand(int Legajo, string Nombre, string Apellido, string? Dni, string? Cuil,
    string Correo, Guid? AreaId, IReadOnlyList<Rol> Roles) : IRequest<EmpleadoDto>;

public sealed class CreateEmpleadoCommandValidator : AbstractValidator<CreateEmpleadoCommand>
{
    public CreateEmpleadoCommandValidator()
    {
        RuleFor(c => c.Legajo).GreaterThan(0).WithMessage("El legajo debe ser mayor a cero.");
        RuleFor(c => c.Nombre).NotEmpty().WithMessage("El nombre es obligatorio.");
        RuleFor(c => c.Apellido).NotEmpty().WithMessage("El apellido es obligatorio.");
        RuleFor(c => c.Correo).NotEmpty().WithMessage("El correo es obligatorio.");
    }
}

public sealed class CreateEmpleadoCommandHandler : IRequestHandler<CreateEmpleadoCommand, EmpleadoDto>
{
    private readonly IEmpleadoRepository _empleados;
    private readonly IAreaRepository _areas;

    public CreateEmpleadoCommandHandler(IEmpleadoRepository empleados, IAreaRepository areas)
    {
        _empleados = empleados;
        _areas = areas;
    }

    public async Task<EmpleadoDto> Handle(CreateEmpleadoCommand request, CancellationToken ct)
    {
        if (await _empleados.ExisteLegajoAsync(request.Legajo, ct))
            throw new ReglaDeNegocioException($"Ya existe un empleado con el legajo {request.Legajo}.");
        if (await _empleados.GetByCorreoAsync(request.Correo, ct) is not null)
            throw new ReglaDeNegocioException("Ya existe un empleado con ese correo.");

        Email correo;
        try
        {
            correo = new Email(request.Correo);
        }
        catch (ArgumentException ex)
        {
            throw new ReglaDeNegocioException(ex.Message);
        }

        var empleado = new Empleado(request.Legajo, request.Nombre, request.Apellido, correo, request.Dni, request.Cuil);
        empleado.AsignarArea(request.AreaId);
        foreach (var rol in request.Roles.Where(r => r != Rol.Empleado))
            empleado.AgregarRol(rol);

        await _empleados.AddAsync(empleado, ct);

        var areas = (await _areas.GetAllAsync(ct)).ToDictionary(a => a.Id);
        return new EmpleadoDto(empleado.Id, empleado.Legajo, empleado.Nombre, empleado.Apellido, empleado.Dni,
            empleado.Cuil, empleado.Correo.Valor, empleado.AreaId,
            empleado.AreaId is { } id && areas.TryGetValue(id, out var area) ? area.Nombre : null, empleado.Activo,
            empleado.Roles.ToList());
    }
}

public sealed record UpdateEmpleadoCommand(Guid Id, string Nombre, string Apellido, string? Dni, string? Cuil,
    Guid? AreaId) : IRequest<EmpleadoDto>;

public sealed class UpdateEmpleadoCommandHandler : IRequestHandler<UpdateEmpleadoCommand, EmpleadoDto>
{
    private readonly IEmpleadoRepository _empleados;
    private readonly IAreaRepository _areas;

    public UpdateEmpleadoCommandHandler(IEmpleadoRepository empleados, IAreaRepository areas)
    {
        _empleados = empleados;
        _areas = areas;
    }

    public async Task<EmpleadoDto> Handle(UpdateEmpleadoCommand request, CancellationToken ct)
    {
        var empleado = await _empleados.GetByIdAsync(request.Id, ct)
            ?? throw new EntidadNoEncontradaException("El empleado no existe.");

        empleado.ActualizarDatos(request.Nombre, request.Apellido, request.Dni, request.Cuil);
        empleado.AsignarArea(request.AreaId);
        await _empleados.UpdateAsync(empleado, ct);

        var areas = (await _areas.GetAllAsync(ct)).ToDictionary(a => a.Id);
        return new EmpleadoDto(empleado.Id, empleado.Legajo, empleado.Nombre, empleado.Apellido, empleado.Dni,
            empleado.Cuil, empleado.Correo.Valor, empleado.AreaId,
            empleado.AreaId is { } id && areas.TryGetValue(id, out var area) ? area.Nombre : null, empleado.Activo,
            empleado.Roles.ToList());
    }
}

public sealed record SetRolesEmpleadoCommand(Guid Id, IReadOnlyList<Rol> Roles) : IRequest<Unit>;

public sealed class SetRolesEmpleadoCommandHandler : IRequestHandler<SetRolesEmpleadoCommand, Unit>
{
    private readonly IEmpleadoRepository _empleados;

    public SetRolesEmpleadoCommandHandler(IEmpleadoRepository empleados) => _empleados = empleados;

    public async Task<Unit> Handle(SetRolesEmpleadoCommand request, CancellationToken ct)
    {
        var empleado = await _empleados.GetByIdAsync(request.Id, ct)
            ?? throw new EntidadNoEncontradaException("El empleado no existe.");

        foreach (var rol in empleado.Roles.Where(r => r != Rol.Empleado).ToList())
            empleado.QuitarRol(rol);

        foreach (var rol in request.Roles.Where(r => r != Rol.Empleado))
            empleado.AgregarRol(rol);

        await _empleados.UpdateAsync(empleado, ct);
        return Unit.Value;
    }
}

public sealed record SetActivoEmpleadoCommand(Guid Id, bool Activo) : IRequest<Unit>;

public sealed class SetActivoEmpleadoCommandHandler : IRequestHandler<SetActivoEmpleadoCommand, Unit>
{
    private readonly IEmpleadoRepository _empleados;

    public SetActivoEmpleadoCommandHandler(IEmpleadoRepository empleados) => _empleados = empleados;

    public async Task<Unit> Handle(SetActivoEmpleadoCommand request, CancellationToken ct)
    {
        var empleado = await _empleados.GetByIdAsync(request.Id, ct)
            ?? throw new EntidadNoEncontradaException("El empleado no existe.");

        if (request.Activo) empleado.Activar();
        else empleado.Desactivar();

        await _empleados.UpdateAsync(empleado, ct);
        return Unit.Value;
    }
}