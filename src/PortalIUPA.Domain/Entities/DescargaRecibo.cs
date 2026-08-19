using PortalIUPA.Domain.Enums;

namespace PortalIUPA.Domain.Entities;

/// <summary>Registro de auditoría inmutable de cada descarga de recibo (legacy: historial del usuario).</summary>
public sealed class DescargaRecibo
{
    public Guid Id { get; private set; }
    public Guid EmpleadoId { get; private set; }
    public Guid PeriodoId { get; private set; }
    public DateTime FechaHora { get; private set; }
    public OrigenDescarga Origen { get; private set; }
    public string? Ip { get; private set; }

    private DescargaRecibo() { }

    public DescargaRecibo(Guid empleadoId, Guid periodoId, OrigenDescarga origen, string? ip = null)
    {
        Id = Guid.NewGuid();
        EmpleadoId = empleadoId;
        PeriodoId = periodoId;
        Origen = origen;
        Ip = ip;
        FechaHora = DateTime.UtcNow;
    }
}