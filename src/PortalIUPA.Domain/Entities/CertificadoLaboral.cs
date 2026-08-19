using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.ValueObjects;

namespace PortalIUPA.Domain.Entities;

/// <summary>Solicitud de certificado laboral (constancia de trabajo / certificación de servicios) autogenerada en PDF.</summary>
public sealed class CertificadoLaboral
{
    public Guid Id { get; private set; }
    public Guid EmpleadoId { get; private set; }
    public TipoCertificado Tipo { get; private set; }
    public DateOnly Desde { get; private set; }
    public DateOnly Hasta { get; private set; }
    public string? Destino { get; private set; }
    public EstadoCertificado Estado { get; private set; }
    public Guid? ArchivoAdjuntoId { get; private set; }
    public DateTime FechaSolicitud { get; private set; }

    private CertificadoLaboral() { }

    public CertificadoLaboral(Guid empleadoId, TipoCertificado tipo, RangoFechas periodo, string? destino = null)
    {
        Id = Guid.NewGuid();
        EmpleadoId = empleadoId;
        Tipo = tipo;
        Desde = periodo.Inicio;
        Hasta = periodo.Fin;
        Destino = destino?.Trim();
        Estado = EstadoCertificado.Solicitado;
        FechaSolicitud = DateTime.UtcNow;
    }

    public RangoFechas Periodo => new(Desde, Hasta);

    public void MarcarGenerado(Guid adjuntoId) => ArchivoAdjuntoId = adjuntoId;

    public void Rechazar() => Estado = EstadoCertificado.Rechazado;
}