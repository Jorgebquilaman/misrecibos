namespace PortalIUPA.Application.DTOs;

public sealed record JornadaDto(DateOnly Fecha, DateTime? Entrada, DateTime? Salida, TimeSpan? Horas, bool EsAnomalia);

public sealed record ResumenJornadasDto(
    int DiasTrabajados,
    int DiasConAnomalia,
    TimeSpan TotalHoras,
    TimeSpan? PromedioHoras);

public sealed record MisFichadasDto(
    int Anio,
    int Mes,
    IReadOnlyList<JornadaDto> Jornadas,
    ResumenJornadasDto Resumen);

public sealed record AsistenciaPorEmpleadoDto(
    Guid EmpleadoId,
    string EmpleadoNombre,
    int Legajo,
    string? Area,
    int DiasTrabajados,
    int DiasConAnomalia,
    TimeSpan TotalHoras);

public sealed record AsistenciaAreaDto(
    string Area,
    DateOnly Desde,
    DateOnly Hasta,
    IReadOnlyList<AsistenciaPorEmpleadoDto> Empleados);

/// <summary>Marca de entrada/salida cargada manualmente (origen "manual").</summary>
public sealed record MarcaManualDto(Guid Id, Guid EmpleadoId, DateTime FechaHora, string Tipo, string Origen);

/// <summary>Empleado autorizado a cargar marcas manuales (rol HomeOffice, activo).</summary>
public sealed record HomeOfficeEmpleadoDto(Guid Id, string Nombre, string Apellido, int Legajo);