using PortalIUPA.Application.DTOs;

namespace PortalIUPA.Application.Common;

/// <summary>Archivo binario generado para descargar (XLSX o PDF).</summary>
public sealed record ArchivoExportado(byte[] Bytes, string ContentType, string NombreArchivo);

/// <summary>Genera la exportación de la lista de empleados en XLSX o PDF.</summary>
public interface IExportadorEmpleados
{
    Task<ArchivoExportado> GenerarAsync(IReadOnlyList<EmpleadoDto> empleados, string formato,
        CancellationToken ct = default);
}