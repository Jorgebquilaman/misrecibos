using PortalIUPA.Application.DTOs;

namespace PortalIUPA.Application.Common;

/// <summary>Genera la exportación de un reporte tabular en XLSX o PDF.</summary>
public interface IExportadorTabular
{
    Task<ArchivoExportado> GenerarAsync(ReporteTabularDto reporte, string formato, CancellationToken ct = default);
}