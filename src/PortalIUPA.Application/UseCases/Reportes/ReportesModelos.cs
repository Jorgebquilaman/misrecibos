using System.Text.Json;
using System.Text.Json.Serialization;

namespace PortalIUPA.Application.UseCases.Reportes;

public static class ReportesJson
{
    public static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
}

public enum TipoDatoReporte { Texto, Numero, Fecha, Booleano }
public enum AgregacionReporte { Suma, Promedio, Maximo, Minimo, Conteo }
public enum OperadorFiltro
{
    Igual, Distinto, Contiene, NoContiene, En,
    Mayor, MayorIgual, Menor, MenorIgual, Entre, Vacio, NoVacio
}
public enum TipoGrafico { Barras, Lineas, Torta }

public sealed record ColumnaMetadata(string Nombre, TipoDatoReporte Tipo);

// ---------- Definición (se persiste como JSON en reportes.definicion) ----------

public sealed record CampoFilasDef(string Campo, string Alias);
public sealed record CampoValorDef(string Campo, string Alias, AgregacionReporte Agregacion);
public sealed record CampoColumnaDef(string Campo, string Alias);
public sealed record CampoRelacionDef(string CampoPadre, string CampoHijo);
public sealed record GraficoDef(string Titulo, TipoGrafico Tipo, string CampoX, IReadOnlyList<string> CamposY);

public sealed record FiltroDef(
    string Campo,
    TipoDatoReporte TipoDato,
    OperadorFiltro Operador,
    string? Etiqueta,
    bool Parametrizable,
    string? Valor,
    string? Valor2,
    IReadOnlyList<string>? Valores);

public sealed record SubreporteDef(Guid ReporteId, string? Nombre, IReadOnlyList<CampoRelacionDef> CamposRelacion);

public sealed record ReporteDefinicionDto(
    IReadOnlyList<CampoFilasDef> Filas,
    IReadOnlyList<CampoValorDef> Valores,
    IReadOnlyList<CampoColumnaDef> Columnas,
    IReadOnlyList<FiltroDef> Filtros,
    IReadOnlyList<GraficoDef> Graficos,
    IReadOnlyList<SubreporteDef> Subreportes,
    string? Orden,
    int? Limite)
{
    public static ReporteDefinicionDto Vacio() => new(
        Array.Empty<CampoFilasDef>(), Array.Empty<CampoValorDef>(), Array.Empty<CampoColumnaDef>(),
        Array.Empty<FiltroDef>(), Array.Empty<GraficoDef>(), Array.Empty<SubreporteDef>(), null, null);
}

// ---------- Resultados ----------

public sealed record FiltroEjecutado(
    string Campo, TipoDatoReporte TipoDato, OperadorFiltro Operador,
    object? Valor = null, object? Valor2 = null, IReadOnlyList<object?>? Valores = null);

public sealed record ResultadoReporteDto(
    IReadOnlyList<string> Columnas,
    IReadOnlyList<Dictionary<string, object?>> Filas,
    IReadOnlyDictionary<string, object?> Totales);

// ---------- Exploración de esquema (asistente visual) ----------

public sealed record TablaListaDto(string Esquema, string Tabla);
public sealed record ColumnaTablaDto(string Nombre, string Tipo);
public sealed record RelacionFkDto(string TablaExterna, string ColLocal, string ColExterna);
public sealed record TablaDetalleDto(string Esquema, string Tabla, IReadOnlyList<ColumnaTablaDto> Columnas,
    IReadOnlyList<RelacionFkDto> Relaciones);

// ---------- Puerto hacia el motor SQL (implementado en Infrastructure) ----------

public interface IMotorReportes
{
    IReadOnlyList<string> ConexionesDisponibles { get; }

    Task<IReadOnlyList<TablaListaDto>> ObtenerTablasAsync(string? conexion = null, CancellationToken ct = default);
    Task<TablaDetalleDto> ObtenerTablaAsync(string esquema, string tabla, string? conexion = null, CancellationToken ct = default);

    Task<IReadOnlyList<ColumnaMetadata>> ObtenerMetadataAsync(string sql, string? conexion = null, CancellationToken ct = default);
    Task<ResultadoReporteDto> EjecutarAsync(
        string sqlBase,
        IReadOnlyList<CampoFilasDef> filas,
        IReadOnlyList<CampoValorDef> valores,
        IReadOnlyList<CampoColumnaDef> columnas,
        IReadOnlyList<FiltroEjecutado> filtros,
        string? orden,
        int? limite,
        string? conexion = null,
        CancellationToken ct = default);
}
