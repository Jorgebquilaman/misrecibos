namespace PortalIUPA.Domain.Entities;

/// <summary>Anexo (archivo) asociado a un antecedente académico del CV.</summary>
public sealed class CvAntecedenteAdjunto
{
    public Guid Id { get; private set; }
    public Guid AntecedenteId { get; private set; }
    public Guid AdjuntoId { get; private set; }
    public DateTime FechaCarga { get; private set; }

    private CvAntecedenteAdjunto() { }

    public CvAntecedenteAdjunto(Guid antecedenteId, Guid adjuntoId)
    {
        Id = Guid.NewGuid();
        AntecedenteId = antecedenteId;
        AdjuntoId = adjuntoId;
        FechaCarga = DateTime.UtcNow;
    }
}
