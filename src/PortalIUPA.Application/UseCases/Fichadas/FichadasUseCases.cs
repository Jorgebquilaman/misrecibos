using MediatR;
using PortalIUPA.Application.Common;
using PortalIUPA.Application.DTOs;
using PortalIUPA.Domain.DomainServices;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Application.UseCases.Fichadas;

/// <summary>"Mis fichadas": jornadas del mes + resumen de horas y anomalías (lectura en vivo del reloj).</summary>
public sealed record GetMisFichadasQuery(Guid EmpleadoId, int Anio, int Mes) : IRequest<MisFichadasDto>;

public sealed class GetMisFichadasQueryHandler : IRequestHandler<GetMisFichadasQuery, MisFichadasDto>
{
    private readonly IEmpleadoRepository _empleados;
    private readonly IRelojDataSource _reloj;
    private readonly IMarcaRelojRepository _marcasManuales;

    public GetMisFichadasQueryHandler(IEmpleadoRepository empleados, IRelojDataSource reloj,
        IMarcaRelojRepository marcasManuales)
    {
        _empleados = empleados;
        _reloj = reloj;
        _marcasManuales = marcasManuales;
    }

    public async Task<MisFichadasDto> Handle(GetMisFichadasQuery request, CancellationToken ct)
    {
        var empleado = await _empleados.GetByIdAsync(request.EmpleadoId, ct)
            ?? throw new EntidadNoEncontradaException("El empleado no existe.");

        var desde = new DateTime(request.Anio, request.Mes, 1);
        var hasta = new DateTime(request.Anio, request.Mes, 1).AddMonths(1).AddSeconds(-1);

        var marcas = await _reloj.ObtenerMarcasAsync(empleado.Legajo, desde, hasta, ct);
        var manuales = await _marcasManuales.GetByEmpleadoBetweenAsync(request.EmpleadoId, desde, hasta, ct);
        var todas = marcas
            .Concat(manuales.Select(m => new MarcaRelojCruda(empleado.Legajo, m.FechaHora, m.Tipo, m.Origen)))
            .GroupBy(m => (m.FechaHora, m.Tipo))
            .Select(g => g.First())
            .OrderBy(m => m.FechaHora)
            .ToList();

        var jornadas = CalculadorJornadas.AgruparEnJornadas(todas);
        var resumen = CalculadorJornadas.CalcularResumen(jornadas);

        return new MisFichadasDto(request.Anio, request.Mes,
            jornadas.Select(j => new JornadaDto(j.Fecha, j.Entrada, j.Salida, j.Horas, j.EsAnomalia)).ToList(),
            new ResumenJornadasDto(resumen.DiasTrabajados, resumen.DiasConAnomalia, resumen.TotalHoras,
                resumen.PromedioHoras));
    }
}

/// <summary>Panel del empleador: asistencia de un área (incluye subáreas) en un rango (lectura en vivo del reloj).</summary>
public sealed record GetAsistenciaPorAreaQuery(Guid? AreaId, DateOnly Desde, DateOnly Hasta)
    : IRequest<AsistenciaAreaDto>;

public sealed class GetAsistenciaPorAreaQueryHandler : IRequestHandler<GetAsistenciaPorAreaQuery, AsistenciaAreaDto>
{
    private readonly IAreaRepository _areas;
    private readonly IEmpleadoRepository _empleados;
    private readonly IRelojDataSource _reloj;

    public GetAsistenciaPorAreaQueryHandler(IAreaRepository areas, IEmpleadoRepository empleados,
        IRelojDataSource reloj)
    {
        _areas = areas;
        _empleados = empleados;
        _reloj = reloj;
    }

    public async Task<AsistenciaAreaDto> Handle(GetAsistenciaPorAreaQuery request, CancellationToken ct)
    {
        var todasLasAreas = await _areas.GetAllAsync(ct);
        var areaIds = new HashSet<Guid>();

        if (request.AreaId is null)
        {
            foreach (var area in todasLasAreas)
                areaIds.Add(area.Id);
        }
        else
        {
            var area = todasLasAreas.FirstOrDefault(a => a.Id == request.AreaId)
                ?? throw new EntidadNoEncontradaException("El área no existe.");
            areaIds.Add(area.Id);
            AgregarSubareas(todasLasAreas, area.Id, areaIds);
        }

        var desde = request.Desde.ToDateTime(TimeOnly.MinValue);
        var hasta = request.Hasta.ToDateTime(TimeOnly.MaxValue);
        var marcas = await _reloj.ObtenerMarcasAsync(null, desde, hasta, ct);

        var empleadosPorLegajo = (await _empleados.GetAllAsync(ct))
            .Where(e => e.AreaId is { } areaId && areaIds.Contains(areaId))
            .ToDictionary(e => e.Legajo);

        var resultado = new List<AsistenciaPorEmpleadoDto>();
        foreach (var grupo in marcas.Where(m => empleadosPorLegajo.ContainsKey(m.Legajo)).GroupBy(m => m.Legajo))
        {
            var empleado = empleadosPorLegajo[grupo.Key];
            var resumen = CalculadorJornadas.CalcularResumen(CalculadorJornadas.AgruparEnJornadas(grupo));
            resultado.Add(new AsistenciaPorEmpleadoDto(empleado.Id, empleado.NombreCompleto, empleado.Legajo,
                todasLasAreas.FirstOrDefault(a => a.Id == empleado.AreaId)?.Nombre,
                resumen.DiasTrabajados, resumen.DiasConAnomalia, resumen.TotalHoras));
        }

        return new AsistenciaAreaDto(request.AreaId is null ? "Toda la institución" : "Área seleccionada",
            request.Desde, request.Hasta,
            resultado.OrderByDescending(r => r.TotalHoras).ToList());
    }

    private static void AgregarSubareas(IReadOnlyList<Area> areas, Guid padreId, HashSet<Guid> areaIds)
    {
        foreach (var area in areas.Where(a => a.AreaPadreId == padreId))
        {
            if (areaIds.Add(area.Id))
                AgregarSubareas(areas, area.Id, areaIds);
        }
    }
}

/// <summary>Roles que pueden cargar marcas manuales para cualquier empleado (no solo para sí mismos).</summary>
public static class RolesAutorizadosMarcaManual
{
    public static readonly IReadOnlySet<string> Staff =
        new HashSet<string> { "Responsable", "Rrhh", "Administrador", "Direccion" };
}

/// <summary>Carga manual de una marca de ingreso/egreso. La autoriza quien tiene rol HomeOffice (solo para sí mismo)
/// o roles de staff (Responsable/Rrhh/Administrador/Direccion) para cualquier empleado.</summary>
public sealed record CrearMarcaManualCommand(
    Guid EmpleadoIdSolicitante,
    IReadOnlyCollection<string> RolesSolicitante,
    Guid? EmpleadoId,
    DateTime FechaHora,
    string Tipo) : IRequest<MarcaManualDto>;

public sealed class CrearMarcaManualCommandHandler : IRequestHandler<CrearMarcaManualCommand, MarcaManualDto>
{
    private readonly IEmpleadoRepository _empleados;
    private readonly IMarcaRelojRepository _marcas;

    public CrearMarcaManualCommandHandler(IEmpleadoRepository empleados, IMarcaRelojRepository marcas)
    {
        _empleados = empleados;
        _marcas = marcas;
    }

    public async Task<MarcaManualDto> Handle(CrearMarcaManualCommand request, CancellationToken ct)
    {
        var esStaff = request.RolesSolicitante.Any(r => RolesAutorizadosMarcaManual.Staff.Contains(r));
        var esHomeOffice = request.RolesSolicitante.Contains("HomeOffice");
        var destino = request.EmpleadoId ?? request.EmpleadoIdSolicitante;

        if (esStaff)
        {
            // staff: puede cargar para cualquier empleado.
        }
        else if (esHomeOffice && destino == request.EmpleadoIdSolicitante)
        {
            // homeoffice: solo para sí mismo.
        }
        else
        {
            throw new ReglaDeNegocioException(
                "No tenés permiso para cargar marcas manuales. Solo el personal con rol HomeOffice o de administración puede hacerlo.");
        }

        var tipo = request.Tipo.Trim().ToLowerInvariant() switch
        {
            "entrada" => TipoMarca.Entrada,
            "salida" => TipoMarca.Salida,
            _ => throw new ReglaDeNegocioException("El tipo de marca debe ser 'entrada' o 'salida'.")
        };

        if (request.FechaHora > DateTime.Now.AddMinutes(5))
            throw new ReglaDeNegocioException("No se puede cargar una marca con fecha futura.");

        var empleado = await _empleados.GetByIdAsync(destino, ct)
            ?? throw new EntidadNoEncontradaException("El empleado no existe.");
        if (!empleado.Activo)
            throw new ReglaDeNegocioException("El empleado no está activo.");

        if (await _marcas.ExisteAsync(destino, request.FechaHora, ct))
            throw new ReglaDeNegocioException("Ya existe una marca para ese empleado en esa fecha y hora.");

        var marca = new MarcaReloj(destino, request.FechaHora, tipo, "manual");
        await _marcas.AddRangeAsync(new[] { marca }, ct);

        return new MarcaManualDto(marca.Id, marca.EmpleadoId, marca.FechaHora,
            marca.Tipo == TipoMarca.Entrada ? "entrada" : "salida", marca.Origen ?? "manual");
    }
}

/// <summary>Lista las marcas manuales de un empleado (para staff puede ser cualquier empleado; para el resto, solo las propias).</summary>
public sealed record GetMarcasManualesQuery(
    Guid EmpleadoIdSolicitante,
    IReadOnlyCollection<string> RolesSolicitante,
    Guid? EmpleadoId,
    DateOnly Desde,
    DateOnly Hasta) : IRequest<IReadOnlyList<MarcaManualDto>>;

public sealed class GetMarcasManualesQueryHandler : IRequestHandler<GetMarcasManualesQuery, IReadOnlyList<MarcaManualDto>>
{
    private readonly IMarcaRelojRepository _marcas;

    public GetMarcasManualesQueryHandler(IMarcaRelojRepository marcas) => _marcas = marcas;

    public async Task<IReadOnlyList<MarcaManualDto>> Handle(GetMarcasManualesQuery request, CancellationToken ct)
    {
        var esStaff = request.RolesSolicitante.Any(r => RolesAutorizadosMarcaManual.Staff.Contains(r));
        var destino = esStaff && request.EmpleadoId is { } id ? id : request.EmpleadoIdSolicitante;

        var desde = request.Desde.ToDateTime(TimeOnly.MinValue);
        var hasta = request.Hasta.ToDateTime(TimeOnly.MaxValue);

        var marcas = await _marcas.GetByEmpleadoBetweenAsync(destino, desde, hasta, ct);
        return marcas
            .Where(m => m.Origen == "manual")
            .OrderByDescending(m => m.FechaHora)
            .Select(m => new MarcaManualDto(m.Id, m.EmpleadoId, m.FechaHora,
                m.Tipo == TipoMarca.Entrada ? "entrada" : "salida", m.Origen ?? "manual"))
            .ToList();
    }
}

/// <summary>Empleados autorizados a cargar marcas manuales (rol HomeOffice y activos).</summary>
public sealed record GetHomeOfficeEmpleadosQuery : IRequest<IReadOnlyList<HomeOfficeEmpleadoDto>>;

public sealed class GetHomeOfficeEmpleadosQueryHandler
    : IRequestHandler<GetHomeOfficeEmpleadosQuery, IReadOnlyList<HomeOfficeEmpleadoDto>>
{
    private readonly IEmpleadoRepository _empleados;

    public GetHomeOfficeEmpleadosQueryHandler(IEmpleadoRepository empleados) => _empleados = empleados;

    public async Task<IReadOnlyList<HomeOfficeEmpleadoDto>> Handle(GetHomeOfficeEmpleadosQuery request, CancellationToken ct)
    {
        var empleados = await _empleados.GetAllAsync(ct);
        return empleados
            .Where(e => e.Activo && e.TieneRol(Rol.HomeOffice))
            .OrderBy(e => e.Apellido)
            .ThenBy(e => e.Nombre)
            .Select(e => new HomeOfficeEmpleadoDto(e.Id, e.Nombre, e.Apellido, e.Legajo))
            .ToList();
    }
}