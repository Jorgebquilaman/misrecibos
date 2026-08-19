namespace PortalIUPA.Domain.Ports;

/// <summary>Período de liquidación tal como existe en el sistema de liquidación de haberes (SIU-Mapuche).</summary>
/// <param name="NroLiq">Identificador de la liquidación (mapuche.dh22.nro_liqui).</param>
/// <param name="Codigo">Código derivado del período (ej. "2026-06").</param>
/// <param name="Descripcion">Descripción de la liquidación.</param>
/// <param name="Disponible">true si la liquidación está cerrada y se puede descargar el recibo.</param>
public sealed record PeriodoExterno(int NroLiq, string Codigo, string Descripcion, bool Disponible);

public interface IPeriodosDataSource
{
    Task<IReadOnlyList<PeriodoExterno>> ObtenerPeriodosAsync(CancellationToken ct = default);
}