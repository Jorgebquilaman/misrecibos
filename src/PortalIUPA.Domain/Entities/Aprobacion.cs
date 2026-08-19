using PortalIUPA.Domain.Enums;

namespace PortalIUPA.Domain.Entities;

/// <summary>Decisión de un nivel de la cadena de aprobación. Inmutable: solo se crea, nunca se modifica.</summary>
public sealed class Aprobacion
{
    public Guid Id { get; private set; }
    public Guid SolicitudId { get; private set; }
    public Guid NivelAprobacionId { get; private set; }
    public Guid AprobadorId { get; private set; }
    public ResultadoAprobacion Resultado { get; private set; }
    public string? Comentario { get; private set; }
    public DateTime FechaHora { get; private set; }

    private Aprobacion() { }

    public Aprobacion(Guid solicitudId, Guid nivelAprobacionId, Guid aprobadorId, ResultadoAprobacion resultado,
        string? comentario = null)
    {
        Id = Guid.NewGuid();
        SolicitudId = solicitudId;
        NivelAprobacionId = nivelAprobacionId;
        AprobadorId = aprobadorId;
        Resultado = resultado;
        Comentario = comentario?.Trim();
        FechaHora = DateTime.UtcNow;
    }
}