namespace PortalIUPA.Application.UseCases.Reportes;

// ---------- Definición del dashboard (se persiste como JSON en dashboards.definicion) ----------

/// <summary>Filtro global del dashboard: se aplica a todos los controles.</summary>
public sealed record FiltroDashboardDef(
    string Campo,
    TipoDatoReporte TipoDato,
    OperadorFiltro Operador,
    string? Etiqueta,
    string? Valor,
    string? Valor2,
    IReadOnlyList<string>? Valores);

public sealed record KpiDef(
    string Titulo,
    string Campo,
    AgregacionReporte Agregacion,
    string Formato,            // numero | moneda | porcentaje
    bool EsKpo,
    decimal? Objetivo,
    string? Color);

public sealed record GraficoDashboardDef(
    string Titulo,
    string Tipo,               // Barras | Lineas | Torta | Area | Dona
    string CampoX,
    string CampoY,
    AgregacionReporte Agregacion,
    bool OrdenarValorDesc,
    string Ancho);             // completo | mitad

public sealed record TablaDashboardDef(string Titulo, int Limite);

public sealed record DashboardDefinicionDto(
    IReadOnlyList<FiltroDashboardDef> Filtros,
    IReadOnlyList<KpiDef> Kpis,
    IReadOnlyList<GraficoDashboardDef> Graficos,
    IReadOnlyList<TablaDashboardDef> Tablas,
    int? Limite)
{
    public static DashboardDefinicionDto Vacio() => new(
        Array.Empty<FiltroDashboardDef>(), Array.Empty<KpiDef>(),
        Array.Empty<GraficoDashboardDef>(), Array.Empty<TablaDashboardDef>(), 5000);
}

// ---------- Resultado de ejecución ----------

public sealed record KpiResultadoDto(string Titulo, decimal Valor, string Formato, bool EsKpo, decimal? Objetivo, string? Color);
public sealed record GraficoResultadoDto(string Titulo, string Tipo, bool AnchoCompleto, ResultadoReporteDto Datos);
public sealed record TablaResultadoDashboardDto(string Titulo, ResultadoReporteDto Datos);

public sealed record ResultadoDashboardDto(
    IReadOnlyList<KpiResultadoDto> Kpis,
    IReadOnlyList<GraficoResultadoDto> Graficos,
    IReadOnlyList<TablaResultadoDashboardDto> Tablas);
