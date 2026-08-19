using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.ValueObjects;

namespace PortalIUPA.Domain.Entities;

/// <summary>Solicitud de licencia de un empleado. Avanza por la cadena de aprobación del tipo.</summary>
public sealed class SolicitudLicencia
{
    public Guid Id { get; private set; }
    public Guid EmpleadoId { get; private set; }
    public Guid TipoLicenciaId { get; private set; }
    public DateOnly FechaInicio { get; private set; }
    public DateOnly FechaFin { get; private set; }
    public string? Asunto { get; private set; }
    public string? Motivo { get; private set; }
    public Guid? AdjuntoId { get; private set; }
    public EstadoSolicitud Estado { get; private set; }
    public DateTime FechaSolicitud { get; private set; }

    private SolicitudLicencia() { }

    public SolicitudLicencia(Guid empleadoId, Guid tipoLicenciaId, RangoFechas rango, string? asunto = null,
        string? motivo = null, Guid? adjuntoId = null)
    {
        if (rango.Dias <= 0) throw new ArgumentException("El rango de fechas es inválido.", nameof(rango));

        Id = Guid.NewGuid();
        EmpleadoId = empleadoId;
        TipoLicenciaId = tipoLicenciaId;
        FechaInicio = rango.Inicio;
        FechaFin = rango.Fin;
        Asunto = asunto?.Trim();
        Motivo = motivo?.Trim();
        AdjuntoId = adjuntoId;
        Estado = EstadoSolicitud.EnEspera;
        FechaSolicitud = DateTime.UtcNow;
    }

    public RangoFechas Rango => new(FechaInicio, FechaFin);

    public int Dias => Rango.Dias;

    /// <summary>Regla de dominio: no se puede pedir licencia solapada con otra no terminada.</summary>
    public bool SolapaCon(RangoFechas otroRango) => Rango.SolapaCon(otroRango);

    public void Cancelar()
    {
        if (Estado is EstadoSolicitud.Aprobada or EstadoSolicitud.Desaprobada)
            throw new InvalidOperationException("No se puede cancelar una solicitud ya resuelta.");
        Estado = EstadoSolicitud.Cancelada;
    }

    public void ActualizarEstado(EstadoSolicitud estado) => Estado = estado;
}