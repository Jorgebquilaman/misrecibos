namespace PortalIUPA.Application.DTOs;

/// <summary>Reporte tabular genérico (para exportar a PDF/XLSX): título, subtítulo, columnas y filas.</summary>
public sealed record ReporteTabularDto(
    string Titulo,
    string Subtitulo,
    IReadOnlyList<string> Columnas,
    IReadOnlyList<IReadOnlyList<string>> Filas,
    string? Resumen = null);