using PortalIUPA.Domain.Enums;

namespace PortalIUPA.Application.DTOs;

public sealed record AreaDto(Guid Id, string Nombre, string Codigo, Guid? AreaPadreId, bool Activa);

public sealed record EmpleadoDto(
    Guid Id,
    int Legajo,
    string Nombre,
    string Apellido,
    string? Dni,
    string? Cuil,
    string Correo,
    Guid? AreaId,
    string? AreaNombre,
    bool Activo,
    IReadOnlyList<Rol> Roles);

public sealed record PaginaEmpleadosDto(
    IReadOnlyList<EmpleadoDto> Items,
    int Total,
    int Pagina,
    int Tamano);

public sealed record EmpleadoDetalleDto(
    EmpleadoDto Empleado,
    Guid? ResponsableId,
    string? ResponsableNombre,
    bool AutorizaMarcas,
    IReadOnlyList<EmpleadoDto> EmpleadosACargo);

public sealed record RelacionACargoDto(Guid Id, Guid ResponsableId, string ResponsableNombre, Guid EmpleadoId,
    string EmpleadoNombre, bool AutorizaMarcas, DateOnly FechaDesde, DateOnly? FechaHasta);

public sealed record OrganigramaNodoDto(
    Guid Id,
    string Nombre,
    string Codigo,
    IReadOnlyList<OrganigramaNodoDto> Hijas,
    IReadOnlyList<EmpleadoDto> Empleados);