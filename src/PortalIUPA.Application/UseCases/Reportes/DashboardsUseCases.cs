using System.Text.Json;
using MediatR;
using PortalIUPA.Application.Common;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Application.UseCases.Reportes;

// ---------------- Truncado de categorías (top N + Otros) ----------------

public static class TruncadoGraficos
{
    public const int MaxCategorias = 15;

    /// <summary>Deja el top N por valor y agrupa el resto en "Otros" (solo tipos discretos).</summary>
    public static ResultadoReporteDto Truncar(ResultadoReporteDto res, string tipoGrafico)
    {
        if (tipoGrafico is not ("Barras" or "Torta" or "Dona")) return res;
        if (res.Filas.Count <= MaxCategorias || res.Columnas.Count == 0) return res;

        var top = res.Filas.Take(MaxCategorias).ToList();
        decimal sumaOtros = 0;
        foreach (var f in res.Filas.Skip(MaxCategorias))
            if (f.TryGetValue("valor", out var v) && v is decimal d) sumaOtros += d;
            else if (f.TryGetValue("valor", out var v2) && v2 is IConvertible conv) sumaOtros += Convert.ToDecimal(conv);
        top.Add(new Dictionary<string, object?> { [res.Columnas[0]] = "Otros", ["valor"] = sumaOtros });
        return res with { Filas = top };
    }
}

// ---------------- Guardar (crear o actualizar) ----------------

public sealed record GuardarDashboardCommand(
    Guid? Id,
    string Nombre,
    string? Descripcion,
    string QuerySql,
    string? Conexion,
    DashboardDefinicionDto Definicion,
    string UsuarioEmail) : IRequest<Guid>;

public sealed class GuardarDashboardCommandHandler : IRequestHandler<GuardarDashboardCommand, Guid>
{
    private readonly IDashboardRepository _repo;
    private readonly IMotorReportes _motor;

    public GuardarDashboardCommandHandler(IDashboardRepository repo, IMotorReportes motor)
    {
        _repo = repo;
        _motor = motor;
    }

    public async Task<Guid> Handle(GuardarDashboardCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.UsuarioEmail))
            throw new AccesoDenegadoException("No se pudo identificar el usuario.");

        if (!string.IsNullOrWhiteSpace(request.Conexion) &&
            !_motor.ConexionesDisponibles.Contains(request.Conexion, StringComparer.OrdinalIgnoreCase))
            throw new ReglaDeNegocioException($"La conexión '{request.Conexion}' no está configurada en el servidor.");

        // Validamos la consulta y que cada campo usado por los controles exista en ella.
        var columnas = await _motor.ObtenerMetadataAsync(request.QuerySql, request.Conexion, ct);
        var nombres = columnas.Select(c => c.Nombre).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var referenciados = request.Definicion.Kpis.Select(k => k.Campo)
            .Concat(request.Definicion.Graficos.SelectMany(g => new[] { g.CampoX, g.CampoY }))
            .Concat(request.Definicion.Filtros.Select(f => f.Campo))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .Where(c => !nombres.Contains(c))
            .ToList();
        if (referenciados.Count > 0)
            throw new ReglaDeNegocioException(
                $"El dashboard usa campos que no están en la consulta: {string.Join(", ", referenciados)}. " +
                $"Columnas disponibles: {string.Join(", ", columnas.Select(c => c.Nombre))}.");

        var json = JsonSerializer.Serialize(request.Definicion, ReportesJson.Opciones);

        if (request.Id is null)
        {
            var dashboard = new PortalIUPA.Domain.Entities.DashboardDefinicion(
                request.Nombre, request.QuerySql, json, request.UsuarioEmail, request.Descripcion, request.Conexion);
            await _repo.AddAsync(dashboard, ct);
            return dashboard.Id;
        }

        var existente = await _repo.GetByIdAsync(request.Id.Value, ct)
                        ?? throw new EntidadNoEncontradaException("Dashboard no encontrado.");
        existente.Editar(request.Nombre, request.Descripcion, request.QuerySql, json, request.Conexion);
        await _repo.UpdateAsync(existente, ct);
        return existente.Id;
    }
}

// ---------------- Eliminar ----------------

public sealed record EliminarDashboardCommand(Guid Id, string UsuarioEmail) : IRequest;

public sealed class EliminarDashboardCommandHandler : IRequestHandler<EliminarDashboardCommand>
{
    private readonly IDashboardRepository _repo;
    public EliminarDashboardCommandHandler(IDashboardRepository repo) => _repo = repo;

    public async Task Handle(EliminarDashboardCommand request, CancellationToken ct)
    {
        var dashboard = await _repo.GetByIdAsync(request.Id, ct)
                        ?? throw new EntidadNoEncontradaException("Dashboard no encontrado.");
        await _repo.DeleteAsync(dashboard, ct);
    }
}

// ---------------- Listado ----------------

public sealed record DashboardResumenDto(
    Guid Id, string Nombre, string? Descripcion, bool Activo, string CreadoPorEmail, bool PuedeEditar);

public sealed record ListarDashboardsQuery(string Email, IReadOnlyList<string> Roles)
    : IRequest<IReadOnlyList<DashboardResumenDto>>;

public sealed class ListarDashboardsQueryHandler : IRequestHandler<ListarDashboardsQuery, IReadOnlyList<DashboardResumenDto>>
{
    private readonly IDashboardRepository _repo;
    public ListarDashboardsQueryHandler(IDashboardRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<DashboardResumenDto>> Handle(ListarDashboardsQuery request, CancellationToken ct)
    {
        var esAdmin = request.Roles.Contains("Administrador", StringComparer.OrdinalIgnoreCase);
        var esRrhh = esAdmin || request.Roles.Contains("Rrhh", StringComparer.OrdinalIgnoreCase);
        if (!esRrhh)
            throw new AccesoDenegadoException("Solo Rrhh y Administradores pueden ver los dashboards.");

        var todos = await _repo.GetAllAsync(ct);
        return todos
            .Where(d => esAdmin || d.Activo)
            .Select(d => new DashboardResumenDto(
                d.Id, d.Nombre, d.Descripcion, d.Activo, d.CreadoPorEmail, d.PuedeEditar(request.Email, request.Roles)))
            .ToList();
    }
}

// ---------------- Detalle ----------------

public sealed record DashboardCompletoDto(
    Guid Id, string Nombre, string? Descripcion, string QuerySql, bool Activo,
    string CreadoPorEmail, bool PuedeEditar, string Conexion, DashboardDefinicionDto Definicion);

public sealed record ObtenerDashboardQuery(Guid Id, string Email, IReadOnlyList<string> Roles)
    : IRequest<DashboardCompletoDto>;

public sealed class ObtenerDashboardQueryHandler : IRequestHandler<ObtenerDashboardQuery, DashboardCompletoDto>
{
    private readonly IDashboardRepository _repo;
    public ObtenerDashboardQueryHandler(IDashboardRepository repo) => _repo = repo;

    public async Task<DashboardCompletoDto> Handle(ObtenerDashboardQuery request, CancellationToken ct)
    {
        var dashboard = await _repo.GetByIdAsync(request.Id, ct)
                        ?? throw new EntidadNoEncontradaException("Dashboard no encontrado.");

        var esAdmin = request.Roles.Contains("Administrador", StringComparer.OrdinalIgnoreCase);
        var esRrhh = esAdmin || request.Roles.Contains("Rrhh", StringComparer.OrdinalIgnoreCase);
        if (!esRrhh)
            throw new AccesoDenegadoException("Solo Rrhh y Administradores pueden ver los dashboards.");

        return new DashboardCompletoDto(
            dashboard.Id, dashboard.Nombre, dashboard.Descripcion, dashboard.QuerySql, dashboard.Activo,
            dashboard.CreadoPorEmail, dashboard.PuedeEditar(request.Email, request.Roles),
            dashboard.Conexion, Deserializar(dashboard.DefinicionJson));
    }

    internal static DashboardDefinicionDto Deserializar(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<DashboardDefinicionDto>(json, ReportesJson.Opciones)
                   ?? DashboardDefinicionDto.Vacio();
        }
        catch (JsonException)
        {
            return DashboardDefinicionDto.Vacio();
        }
    }
}

// ---------------- Ejecución ----------------

public sealed record EjecutarDashboardQuery(
    Guid Id,
    Dictionary<string, JsonElement>? Valores,
    string Email,
    IReadOnlyList<string> Roles) : IRequest<ResultadoDashboardDto>;

public sealed class EjecutarDashboardQueryHandler : IRequestHandler<EjecutarDashboardQuery, ResultadoDashboardDto>
{
    private readonly IDashboardRepository _repo;
    private readonly IMotorReportes _motor;

    public EjecutarDashboardQueryHandler(IDashboardRepository repo, IMotorReportes motor)
    {
        _repo = repo;
        _motor = motor;
    }

    public async Task<ResultadoDashboardDto> Handle(EjecutarDashboardQuery request, CancellationToken ct)
    {
        var dashboard = await _repo.GetByIdAsync(request.Id, ct)
                        ?? throw new EntidadNoEncontradaException("Dashboard no encontrado.");

        var esAdmin = request.Roles.Contains("Administrador", StringComparer.OrdinalIgnoreCase);
        var esRrhh = esAdmin || request.Roles.Contains("Rrhh", StringComparer.OrdinalIgnoreCase);
        if (!esRrhh)
            throw new AccesoDenegadoException("Solo Rrhh y Administradores pueden ver los dashboards.");
        if (!dashboard.Activo && !esAdmin)
            throw new ReglaDeNegocioException("El dashboard está inactivo.");

        var def = ObtenerDashboardQueryHandler.Deserializar(dashboard.DefinicionJson);
        var filtros = ExtensionesFiltrosDashboard.ResolverFiltrosDashboard(def.Filtros, request.Valores);

        // KPIs / KPOs: agregación sin agrupamiento.
        var kpis = new List<KpiResultadoDto>();
        foreach (var kpi in def.Kpis)
        {
            var res = await _motor.EjecutarAsync(dashboard.QuerySql, Array.Empty<CampoFilasDef>(),
                new[] { new CampoValorDef(kpi.Campo, "valor", kpi.Agregacion) },
                Array.Empty<CampoColumnaDef>(), filtros, null, null, dashboard.Conexion, ct);
            var valor = res.Filas.Count > 0 && res.Filas[0].TryGetValue("valor", out var v) && v is IConvertible conv
                ? Convert.ToDecimal(conv)
                : 0m;
            kpis.Add(new KpiResultadoDto(kpi.Titulo, valor, kpi.Formato, kpi.EsKpo, kpi.Objetivo, kpi.Color));
        }

        // Gráficos: agrupamiento por campoX + agregación del campoY.
        var graficos = new List<GraficoResultadoDto>();
        foreach (var g in def.Graficos)
        {
            var res = await _motor.EjecutarAsync(dashboard.QuerySql,
                new[] { new CampoFilasDef(g.CampoX, g.CampoX) },
                new[] { new CampoValorDef(g.CampoY, "valor", g.Agregacion) },
                Array.Empty<CampoColumnaDef>(), filtros, null, def.Limite ?? 5000, dashboard.Conexion, ct);
            if (g.OrdenarValorDesc && res.Filas.Count > 0)
            {
                res = res with { Filas = res.Filas.OrderByDescending(f =>
                    f.TryGetValue("valor", out var v) && v is IComparable c ? c : 0).ToList() };
            }
            res = TruncadoGraficos.Truncar(res, g.Tipo);
            graficos.Add(new GraficoResultadoDto(g.Titulo, g.Tipo, g.Ancho == "completo", res));
        }

        // Tablas: detalle con los filtros aplicados.
        var tablas = new List<TablaResultadoDashboardDto>();
        foreach (var t in def.Tablas)
        {
            var res = await _motor.EjecutarAsync(dashboard.QuerySql, Array.Empty<CampoFilasDef>(),
                Array.Empty<CampoValorDef>(), Array.Empty<CampoColumnaDef>(), filtros,
                null, Math.Min(t.Limite, 200), dashboard.Conexion, ct);
            tablas.Add(new TablaResultadoDashboardDto(t.Titulo, res));
        }

        return new ResultadoDashboardDto(kpis, graficos, tablas);
    }
}

// ---------------- Vista previa (borrador) ----------------

public sealed record VistaPreviaDashboardQuery(
    string QuerySql,
    string? Conexion,
    DashboardDefinicionDto Definicion,
    Dictionary<string, JsonElement>? Valores,
    string Email,
    IReadOnlyList<string> Roles) : IRequest<ResultadoDashboardDto>;

public sealed class VistaPreviaDashboardQueryHandler : IRequestHandler<VistaPreviaDashboardQuery, ResultadoDashboardDto>
{
    private readonly IMotorReportes _motor;
    public VistaPreviaDashboardQueryHandler(IMotorReportes motor) => _motor = motor;

    public async Task<ResultadoDashboardDto> Handle(VistaPreviaDashboardQuery request, CancellationToken ct)
    {
        var def = request.Definicion;
        var filtros = ExtensionesFiltrosDashboard.ResolverFiltrosDashboard(def.Filtros, request.Valores);

        var kpis = new List<KpiResultadoDto>();
        foreach (var kpi in def.Kpis)
        {
            var res = await _motor.EjecutarAsync(request.QuerySql, Array.Empty<CampoFilasDef>(),
                new[] { new CampoValorDef(kpi.Campo, "valor", kpi.Agregacion) },
                Array.Empty<CampoColumnaDef>(), filtros, null, null, request.Conexion, ct);
            var valor = res.Filas.Count > 0 && res.Filas[0].TryGetValue("valor", out var v) && v is IConvertible conv
                ? Convert.ToDecimal(conv) : 0m;
            kpis.Add(new KpiResultadoDto(kpi.Titulo, valor, kpi.Formato, kpi.EsKpo, kpi.Objetivo, kpi.Color));
        }

        var graficos = new List<GraficoResultadoDto>();
        foreach (var g in def.Graficos)
        {
            var res = await _motor.EjecutarAsync(request.QuerySql,
                new[] { new CampoFilasDef(g.CampoX, g.CampoX) },
                new[] { new CampoValorDef(g.CampoY, "valor", g.Agregacion) },
                Array.Empty<CampoColumnaDef>(), filtros, null, def.Limite ?? 500, request.Conexion, ct);
            if (g.OrdenarValorDesc && res.Filas.Count > 0)
                res = res with { Filas = res.Filas.OrderByDescending(f =>
                    f.TryGetValue("valor", out var v) && v is IConvertible conv ? conv : 0).ToList() };
            res = TruncadoGraficos.Truncar(res, g.Tipo);
            graficos.Add(new GraficoResultadoDto(g.Titulo, g.Tipo, g.Ancho == "completo", res));
        }

        var tablas = new List<TablaResultadoDashboardDto>();
        foreach (var t in def.Tablas)
        {
            var res = await _motor.EjecutarAsync(request.QuerySql, Array.Empty<CampoFilasDef>(),
                Array.Empty<CampoValorDef>(), Array.Empty<CampoColumnaDef>(), filtros,
                null, Math.Min(t.Limite, 200), request.Conexion, ct);
            tablas.Add(new TablaResultadoDashboardDto(t.Titulo, res));
        }

        return new ResultadoDashboardDto(kpis, graficos, tablas);
    }
}
