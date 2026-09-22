namespace PortalIUPA.Infrastructure.Pdf.Trazabilidad;

/// <summary>
/// Configuración de la trazabilidad de PDFs.
/// </summary>
public sealed class TrazabilidadOptions
{
    /// <summary>
    /// Ruta absoluta o relativa (respecto al directorio de trabajo) del registro JSON de trazabilidad.
    /// Por defecto se ubica bajo la carpeta de storage.
    /// </summary>
    public string RutaRegistro { get; set; } = Path.Combine("storage", "trazabilidad", "trace-registry.json");
}
