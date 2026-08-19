namespace PortalIUPA.Domain.Ports;

/// <summary>
/// Puerta de salida hacia JasperReports Server (REST v2) para generar reportes PDF (recibos de sueldo).
/// Desacopla el dominio de la tecnología del generador de reportes.
/// </summary>
public interface IJasperReportClient
{
    /// <param name="reporteRuta">Ruta del reporte dentro de Jasper, ej. "Mapuche/Reportes/Recibos_de_Sueldo_simple".</param>
    /// <param name="parametros">Parámetros del reporte (nroliq, nroleg_f, nroleg_i, ...).</param>
    /// <returns>Binario del PDF generado.</returns>
    Task<byte[]> GetPdfAsync(string reporteRuta, IReadOnlyDictionary<string, string> parametros,
        CancellationToken ct = default);
}