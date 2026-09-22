using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<GetMisFichadasQueryHandler> _logger;

    public GetMisFichadasQueryHandler(IEmpleadoRepository empleados, IRelojDataSource reloj, IMarcaRelojRepository marcasManuales, ILogger<GetMisFichadasQueryHandler> logger)
    {
        _empleados = empleados;
        _reloj = reloj;
        _marcasManuales = marcasManuales;
        _logger = logger;
    }

    public async Task<MisFichadasDto> Handle(GetMisFichadasQuery request, CancellationToken ct)
    {
        var empleado = await _empleados.GetByIdAsync(request.EmpleadoId, ct)
            ?? throw new EntidadNoEncontradaException("El empleado no existe.");

        var desde = new DateTime(request.Anio, request.Mes, 1);
        var hasta = new DateTime(request.Anio, request.Mes, 1).AddMonths(1).AddSeconds(-1);

        IReadOnlyList<MarcaRelojCruda> marcasReloj;
        try
        {
            marcasReloj = await _reloj.ObtenerMarcasAsync(empleado.Legajo, desde, hasta, ct);
        }
        catch (RelojNoDisponibleException ex)
        {
            _logger.LogWarning(ex, "Reloj no disponible para legajo {Legajo}, devolviendo solo marcas manuales.", empleado.Legajo);
            marcasReloj = Array.Empty<MarcaRelojCruda>();
        }
        var manuales = await _marcasManuales.GetByEmpleadoBetweenAsync(request.EmpleadoId, desde, hasta, ct);
        var todas = marcasReloj
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

/// <summary>Reporte tabular de las propias fichadas del mes (para exportar PDF/Excel).</summary>
public sealed record ExportarMisFichadasQuery(Guid EmpleadoId, int Anio, int Mes) : IRequest<ReporteTabularDto>;

public sealed class ExportarMisFichadasQueryHandler : IRequestHandler<ExportarMisFichadasQuery, ReporteTabularDto>
{
    private static readonly string[] Dias =
        ["Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado"];

    private readonly IEmpleadoRepository _empleados;
    private readonly IRelojDataSource _reloj;
    private readonly IMarcaRelojRepository _marcasManuales;

    private readonly ILogger<ExportarMisFichadasQueryHandler> _logger;

    public ExportarMisFichadasQueryHandler(IEmpleadoRepository empleados, IRelojDataSource reloj,
        IMarcaRelojRepository marcasManuales, ILogger<ExportarMisFichadasQueryHandler> logger)
    {
        _empleados = empleados;
        _reloj = reloj;
        _marcasManuales = marcasManuales;
        _logger = logger;
    }

    public async Task<ReporteTabularDto> Handle(ExportarMisFichadasQuery request, CancellationToken ct)
    {
        var empleado = await _empleados.GetByIdAsync(request.EmpleadoId, ct)
            ?? throw new EntidadNoEncontradaException("El empleado no existe.");

        var desde = new DateTime(request.Anio, request.Mes, 1);
        var hasta = new DateTime(request.Anio, request.Mes, 1).AddMonths(1).AddSeconds(-1);

        IReadOnlyList<MarcaRelojCruda> marcasReloj;
        try
        {
            marcasReloj = await _reloj.ObtenerMarcasAsync(empleado.Legajo, desde, hasta, ct);
        }
        catch (RelojNoDisponibleException ex)
        {
            _logger.LogWarning(ex, "Reloj no disponible para export legajo {Legajo}, exportando solo manuales.", empleado.Legajo);
            marcasReloj = Array.Empty<MarcaRelojCruda>();
        }
        var manuales = await _marcasManuales.GetByEmpleadoBetweenAsync(request.EmpleadoId, desde, hasta, ct);
        var todas = marcasReloj
            .Concat(manuales.Select(m => new MarcaRelojCruda(empleado.Legajo, m.FechaHora, m.Tipo, m.Origen)))
            .GroupBy(m => (m.FechaHora, m.Tipo))
            .Select(g => g.First())
            .OrderBy(m => m.FechaHora);

        var jornadas = CalculadorJornadas.AgruparEnJornadas(todas.ToList());

        var filas = new List<IReadOnlyList<string>>();
        foreach (var j in jornadas)
        {
            filas.Add(
            [
                j.Fecha.ToString("dd/MM/yyyy"), Dias[(int)j.Fecha.DayOfWeek],
                j.Entrada?.ToString("HH:mm") ?? "-", j.Salida?.ToString("HH:mm") ?? "-",
                j.Horas is { } h ? $"{(int)h.TotalHours}:{h.Minutes:00}" : "-",
                j.EsAnomalia ? "Sí" : "-"
            ]);
        }

        var resumen = CalculadorJornadas.CalcularResumen(jornadas);
        var resumenTexto = $"Días trabajados: {resumen.DiasTrabajados} · Horas totales: " +
                           $"{(int)resumen.TotalHoras.TotalHours}:{resumen.TotalHoras.Minutes:00} · " +
                           $"Días con anomalía: {resumen.DiasConAnomalia}";

        var nombreMes = new DateTime(request.Anio, request.Mes, 1).ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-AR"));

        return new ReporteTabularDto("Mis fichadas",
            $"{empleado.Apellido}, {empleado.Nombre} (legajo {empleado.Legajo}) · {nombreMes}",
            ["Fecha", "Día", "Entrada", "Salida", "Horas", "Anomalía"], filas, resumenTexto);
    }
}

/// <summary>Exporta el listado crudo de marcas propias del mes: todas las que están en la base
/// (reloj, manuales del portal y descargadas de relojes ZK), marca por marca.</summary>
public sealed record ExportarMarcasMesQuery(Guid EmpleadoId, int Anio, int Mes) : IRequest<ReporteTabularDto>;

public sealed class ExportarMarcasMesQueryHandler : IRequestHandler<ExportarMarcasMesQuery, ReporteTabularDto>
{
    private static readonly string[] Dias =
        ["Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado"];

    private readonly IEmpleadoRepository _empleados;
    private readonly IRelojDataSource _reloj;
    private readonly IMarcaRelojRepository _marcas;
    private readonly ILogger<ExportarMarcasMesQueryHandler> _logger;

    public ExportarMarcasMesQueryHandler(IEmpleadoRepository empleados, IRelojDataSource reloj,
        IMarcaRelojRepository marcas, ILogger<ExportarMarcasMesQueryHandler> logger)
    {
        _empleados = empleados;
        _reloj = reloj;
        _marcas = marcas;
        _logger = logger;
    }

    public async Task<ReporteTabularDto> Handle(ExportarMarcasMesQuery request, CancellationToken ct)
    {
        var empleado = await _empleados.GetByIdAsync(request.EmpleadoId, ct)
            ?? throw new EntidadNoEncontradaException("El empleado no existe.");

        var desde = new DateTime(request.Anio, request.Mes, 1);
        var hasta = new DateTime(request.Anio, request.Mes, 1).AddMonths(1).AddSeconds(-1);

        IReadOnlyList<MarcaRelojCruda> delReloj;
        try
        {
            delReloj = await _reloj.ObtenerMarcasAsync(empleado.Legajo, desde, hasta, ct);
        }
        catch (RelojNoDisponibleException ex)
        {
            _logger.LogWarning(ex, "Reloj no disponible para legajo {Legajo} al exportar marcas del mes.", empleado.Legajo);
            delReloj = Array.Empty<MarcaRelojCruda>();
        }

        // Todas las marcas de la base del portal (cualquier origen: manual, reloj-zk, reloj-zk-push...)
        var deLaBase = await _marcas.GetByEmpleadoBetweenAsync(request.EmpleadoId, desde, hasta, ct);

        var todas = delReloj
            .Concat(deLaBase.Select(m => new MarcaRelojCruda(empleado.Legajo, m.FechaHora, m.Tipo, m.Origen)))
            .GroupBy(m => (m.FechaHora, m.Tipo))
            .Select(g => g.First())
            .OrderBy(m => m.FechaHora)
            .ToList();

        var filas = new List<IReadOnlyList<string>>();
        foreach (var m in todas)
        {
            filas.Add([
                m.FechaHora.ToString("dd/MM/yyyy"), Dias[(int)m.FechaHora.DayOfWeek],
                m.FechaHora.ToString("HH:mm:ss"),
                m.Tipo == TipoMarca.Entrada ? "Entrada" : "Salida",
                DescribirOrigen(m.Origen)
            ]);
        }

        var resumen = $"Total de marcas: {filas.Count}";

        var nombreMes = desde.ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-AR"));
        return new ReporteTabularDto("Marcas del mes",
            $"{empleado.Apellido}, {empleado.Nombre} (legajo {empleado.Legajo}) · {nombreMes}",
            ["Fecha", "Día", "Hora", "Tipo", "Origen"], filas, resumen);
    }

    private static string DescribirOrigen(string? origen) => origen switch
    {
        "manual" => "Manual del portal",
        "manual-pendiente" => "Manual del portal (pendiente de reloj)",
        "reloj" => "Reloj biométrico",
        "reloj-zk" => "Descarga de reloj ZK",
        "reloj-zk-push" => "Reloj ZK (tiempo real)",
        _ => origen ?? "-"
    };
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
/// o roles de staff (Responsable/Rrhh/Administrador/Direccion) para cualquier empleado. Requiere geolocalización:
/// las coordenadas se almacenan con la marca y se asocia al edificio dentro del radio de detección.</summary>
public sealed record CrearMarcaManualCommand(
    Guid EmpleadoIdSolicitante,
    IReadOnlyCollection<string> RolesSolicitante,
    Guid? EmpleadoId,
    DateTime FechaHora,
    string Tipo,
    double? Latitud = null,
    double? Longitud = null) : IRequest<MarcaManualDto>;

public sealed class CrearMarcaManualCommandHandler : IRequestHandler<CrearMarcaManualCommand, MarcaManualDto>
{
    private readonly IEmpleadoRepository _empleados;
    private readonly IMarcaRelojRepository _marcas;
    private readonly IRelojDataSource _reloj;
    private readonly IEdificioRepository _edificios;
    private readonly ILogger<CrearMarcaManualCommandHandler> _logger;

    public CrearMarcaManualCommandHandler(IEmpleadoRepository empleados, IMarcaRelojRepository marcas,
        IRelojDataSource reloj, IEdificioRepository edificios, ILogger<CrearMarcaManualCommandHandler> logger)
    {
        _empleados = empleados;
        _marcas = marcas;
        _reloj = reloj;
        _edificios = edificios;
        _logger = logger;
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

        // Geolocalización obligatoria para marcas manuales
        if (request.Latitud is not { } latitud || request.Longitud is not { } longitud)
            throw new ReglaDeNegocioException(
                "Es necesario habilitar la geolocalización para registrar marcas manuales. " +
                "Sin ubicación no se puede usar el servicio.");

        var marca = new MarcaReloj(destino, request.FechaHora, tipo, "manual");

        // Asociar al edificio más cercano dentro de su radio de detección
        var edificio = await _edificios.GetAllAsync(ct);
        var masCercano = edificio.Where(e => e.Activo)
            .Select(e => (Edificio: e, Distancia: GeoUtiles.DistanciaMetros(latitud, longitud, e.Latitud, e.Longitud)))
            .Where(x => x.Distancia <= x.Edificio.RadioMetros)
            .OrderBy(x => x.Distancia)
            .FirstOrDefault();
        marca.MarcarUbicacion(latitud, longitud, masCercano.Edificio?.Id, masCercano.Edificio?.Nombre);

        await _marcas.AddRangeAsync(new[] { marca }, ct);

        // La marca debe quedar registrada en la base del reloj (MSSQL). Si no se logra,
        // se revierte la copia local y se informa el error: una marca solo en PostgreSQL no cuenta.
        bool replicada;
        try
        {
            replicada = await _reloj.RegistrarMarcaAsync(empleado.Legajo, request.FechaHora, tipo, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            replicada = false;
            _logger.LogError(ex, "Error al registrar marca manual en MSSQL para legajo {Legajo}.", empleado.Legajo);
        }

        if (!replicada)
        {
            // Reloj no disponible: la marca queda en PostgreSQL como pendiente de sincronización
            // (misma política de mejor esfuerzo que Editar/Eliminar). No se revierte ni se bloquea el alta;
            // el listado la muestra con origen "manual-pendiente" hasta que el reloj la confirme.
            _logger.LogWarning(
                "Marca manual {Id} guardada en PostgreSQL pero NO replicada al reloj (MSSQL), legajo {Legajo}. Queda pendiente de sincronización.",
                marca.Id, empleado.Legajo);
        }

        return new MarcaManualDto(marca.Id, marca.EmpleadoId, marca.FechaHora,
            marca.Tipo == TipoMarca.Entrada ? "entrada" : "salida", marca.Origen ?? "manual",
            marca.Latitud, marca.Longitud, marca.EdificioNombre);
    }
}

/// <summary>Edita una marca manual (fecha/hora y tipo). Staff para cualquier empleado; HomeOffice solo las propias.
/// Replica el cambio en la base del reloj (MSSQL).</summary>
public sealed record EditarMarcaManualCommand(
    Guid EmpleadoIdSolicitante, IReadOnlyCollection<string> RolesSolicitante, Guid MarcaId,
    DateTime NuevaFechaHora, string Tipo) : IRequest<MarcaManualDto>;

public sealed class EditarMarcaManualCommandHandler : IRequestHandler<EditarMarcaManualCommand, MarcaManualDto>
{
    private readonly IEmpleadoRepository _empleados;
    private readonly IMarcaRelojRepository _marcas;
    private readonly IRelojDataSource _reloj;
    private readonly ILogger<EditarMarcaManualCommandHandler> _logger;

    public EditarMarcaManualCommandHandler(IEmpleadoRepository empleados, IMarcaRelojRepository marcas,
        IRelojDataSource reloj, ILogger<EditarMarcaManualCommandHandler> logger)
    {
        _empleados = empleados;
        _marcas = marcas;
        _reloj = reloj;
        _logger = logger;
    }

    public async Task<MarcaManualDto> Handle(EditarMarcaManualCommand request, CancellationToken ct)
    {
        var esStaff = request.RolesSolicitante.Any(r => RolesAutorizadosMarcaManual.Staff.Contains(r));
        var esHomeOffice = request.RolesSolicitante.Contains("HomeOffice");

        var marca = await _marcas.GetByIdAsync(request.MarcaId, ct)
            ?? throw new EntidadNoEncontradaException("La marca no existe.");
        if (marca.Origen != "manual")
            throw new ReglaDeNegocioException("Solo se pueden modificar marcas cargadas manualmente desde el portal.");

        if (!esStaff && !(esHomeOffice && marca.EmpleadoId == request.EmpleadoIdSolicitante))
            throw new ReglaDeNegocioException(
                "No tenés permiso para modificar esta marca. Solo el personal con rol HomeOffice o de administración puede hacerlo.");

        var tipo = request.Tipo.Trim().ToLowerInvariant() switch
        {
            "entrada" => TipoMarca.Entrada,
            "salida" => TipoMarca.Salida,
            _ => throw new ReglaDeNegocioException("El tipo de marca debe ser 'entrada' o 'salida'.")
        };

        if (request.NuevaFechaHora > DateTime.Now.AddMinutes(5))
            throw new ReglaDeNegocioException("No se puede cargar una marca con fecha futura.");

        if (await _marcas.ExisteAsync(marca.EmpleadoId, request.NuevaFechaHora, ct))
            throw new ReglaDeNegocioException("Ya existe otra marca para ese empleado en esa fecha y hora.");

        var fechaVieja = marca.FechaHora;
        var tipoViejo = marca.Tipo;

        marca.Editar(request.NuevaFechaHora, tipo);
        await _marcas.UpdateAsync(marca, ct);

        try
        {
            var legajo = (await _empleados.GetByIdAsync(marca.EmpleadoId, ct))?.Legajo;
            if (legajo is { } legajoValor)
            {
                var ok = await _reloj.EditarMarcaAsync(legajoValor, fechaVieja, tipoViejo,
                    request.NuevaFechaHora, tipo, ct);
                if (!ok)
                    _logger.LogWarning("Marca manual {Id} editada en PostgreSQL pero no se replicó en MSSQL (legajo {Legajo}).", marca.Id, legajoValor);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al replicar edición de marca manual {Id} en MSSQL.", marca.Id);
        }

        return new MarcaManualDto(marca.Id, marca.EmpleadoId, marca.FechaHora,
            marca.Tipo == TipoMarca.Entrada ? "entrada" : "salida", marca.Origen ?? "manual");
    }
}

/// <summary>Elimina una marca manual y su réplica en la base del reloj (MSSQL).</summary>
public sealed record EliminarMarcaManualCommand(
    Guid EmpleadoIdSolicitante, IReadOnlyCollection<string> RolesSolicitante, Guid MarcaId) : IRequest<Unit>;

public sealed class EliminarMarcaManualCommandHandler : IRequestHandler<EliminarMarcaManualCommand, Unit>
{
    private readonly IEmpleadoRepository _empleados;
    private readonly IMarcaRelojRepository _marcas;
    private readonly IRelojDataSource _reloj;
    private readonly ILogger<EliminarMarcaManualCommandHandler> _logger;

    public EliminarMarcaManualCommandHandler(IEmpleadoRepository empleados, IMarcaRelojRepository marcas,
        IRelojDataSource reloj, ILogger<EliminarMarcaManualCommandHandler> logger)
    {
        _empleados = empleados;
        _marcas = marcas;
        _reloj = reloj;
        _logger = logger;
    }

    public async Task<Unit> Handle(EliminarMarcaManualCommand request, CancellationToken ct)
    {
        var esStaff = request.RolesSolicitante.Any(r => RolesAutorizadosMarcaManual.Staff.Contains(r));
        var esHomeOffice = request.RolesSolicitante.Contains("HomeOffice");

        var marca = await _marcas.GetByIdAsync(request.MarcaId, ct)
            ?? throw new EntidadNoEncontradaException("La marca no existe.");
        if (marca.Origen != "manual")
            throw new ReglaDeNegocioException("Solo se pueden eliminar marcas cargadas manualmente desde el portal.");

        if (!esStaff && !(esHomeOffice && marca.EmpleadoId == request.EmpleadoIdSolicitante))
            throw new ReglaDeNegocioException(
                "No tenés permiso para eliminar esta marca. Solo el personal con rol HomeOffice o de administración puede hacerlo.");

        var fechaHora = marca.FechaHora;
        var tipo = marca.Tipo;

        await _marcas.DeleteAsync(marca, ct);

        try
        {
            var legajo = (await _empleados.GetByIdAsync(marca.EmpleadoId, ct))?.Legajo;
            if (legajo is { } legajoValor)
            {
                var ok = await _reloj.EliminarMarcaAsync(legajoValor, fechaHora, tipo, ct);
                if (!ok)
                    _logger.LogWarning("Marca manual {Id} eliminada en PostgreSQL pero no se replicó en MSSQL (legajo {Legajo}).", marca.Id, legajoValor);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al replicar eliminación de marca manual {Id} en MSSQL.", marca.Id);
        }

        return Unit.Value;
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
    private readonly IEmpleadoRepository _empleados;
    private readonly IRelojDataSource _reloj;
    private readonly IMarcaRelojRepository _marcas;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GetMarcasManualesQueryHandler> _logger;

    public GetMarcasManualesQueryHandler(IEmpleadoRepository empleados, IRelojDataSource reloj, IMarcaRelojRepository marcas,
        IMemoryCache cache, ILogger<GetMarcasManualesQueryHandler> logger)
    {
        _empleados = empleados;
        _reloj = reloj;
        _marcas = marcas;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MarcaManualDto>> Handle(GetMarcasManualesQuery request, CancellationToken ct)
    {
        var esStaff = request.RolesSolicitante.Any(r => RolesAutorizadosMarcaManual.Staff.Contains(r));
        var destino = esStaff && request.EmpleadoId is { } id ? id : request.EmpleadoIdSolicitante;

        var desde = request.Desde.ToDateTime(TimeOnly.MinValue);
        var hasta = request.Hasta.ToDateTime(TimeOnly.MaxValue);

        var empleado = await _empleados.GetByIdAsync(destino, ct);
        if (empleado is null) return Array.Empty<MarcaManualDto>();

        // Cache corta de la lectura al reloj: recargar la página dentro del minuto no re-consulta MSSQL
        // por el túnel inestable. Las marcas manuales nuevas siempre vienen de PG.
        async Task<IReadOnlyList<MarcaRelojCruda>> LeerRelojAsync()
        {
            var clave = $"reloj-marcas-{empleado.Legajo}-{desde:yyyyMMdd}-{hasta:yyyyMMdd}";
            if (_cache.TryGetValue(clave, out IReadOnlyList<MarcaRelojCruda>? cacheada) && cacheada is not null)
                return cacheada;
            var leidas = await _reloj.ObtenerMarcasAsync(empleado.Legajo, desde, hasta, ct);
            _cache.Set(clave, leidas, TimeSpan.FromSeconds(60));
            return leidas;
        }

        // Fuente de verdad: MSSQL del reloj (checkinout). Fallback a PG solo si el reloj no responde.
        try
        {
            var marcasReloj = await LeerRelojAsync();
            // Mapa PG para resolver Id y ubicación de las manuales (para que editar/eliminar siga funcionando)
            var manualesPg = (await _marcas.GetByEmpleadoBetweenAsync(destino, desde, hasta, ct))
                .Where(m => m.Origen == "manual")
                .ToList();
            var mapaPg = manualesPg.ToDictionary(m => (m.FechaHora, m.Tipo), m => m);

            var resultado = marcasReloj
                .OrderByDescending(m => m.FechaHora)
                .Select(m =>
                {
                    var tipoStr = m.Tipo == TipoMarca.Entrada ? "entrada" : "salida";
                    // Si es manual y existe en PG, reutilizar su Id y ubicación; sino id sintético
                    var id = mapaPg.TryGetValue((m.FechaHora, m.Tipo), out var pg) ? pg.Id : Guid.NewGuid();
                    var origen = m.Origen ?? (m.Tipo == TipoMarca.Entrada ? "reloj" : "reloj");
                    // Las manuales del portal tienen SENSORID='WEB' / sn='PORTAL-IUPA'
                    if (origen == "WEB") origen = "manual";
                    return new MarcaManualDto(id, destino, m.FechaHora, tipoStr, origen,
                        pg?.Latitud, pg?.Longitud, pg?.EdificioNombre);
                })
                .ToList();

            // Marcas cargadas desde el portal que el reloj aún no confirma (cargadas con el reloj
            // caído): se listan igual, marcadas como pendientes de sincronización.
            var clavesReloj = marcasReloj.Select(m => (m.FechaHora, m.Tipo)).ToHashSet();
            foreach (var m in manualesPg
                         .Where(m => !clavesReloj.Contains((m.FechaHora, m.Tipo)))
                         .OrderByDescending(m => m.FechaHora))
            {
                resultado.Add(new MarcaManualDto(m.Id, m.EmpleadoId, m.FechaHora,
                    m.Tipo == TipoMarca.Entrada ? "entrada" : "salida", "manual-pendiente",
                    m.Latitud, m.Longitud, m.EdificioNombre));
            }

            return resultado.OrderByDescending(m => m.FechaHora).ToList();
        }
        catch (RelojNoDisponibleException ex)
        {
            _logger.LogWarning(ex, "Reloj no disponible para marcas-manuales legajo {Legajo}, fallback a PG.", empleado.Legajo);
            var marcas = await _marcas.GetByEmpleadoBetweenAsync(destino, desde, hasta, ct);
            return marcas
                .Where(m => m.Origen == "manual")
                .OrderByDescending(m => m.FechaHora)
                .Select(m => new MarcaManualDto(m.Id, m.EmpleadoId, m.FechaHora,
                    m.Tipo == TipoMarca.Entrada ? "entrada" : "salida", m.Origen ?? "manual",
                    m.Latitud, m.Longitud, m.EdificioNombre))
                .ToList();
        }
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