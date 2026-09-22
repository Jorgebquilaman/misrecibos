namespace PortalIUPA.Domain.Entities;

/// <summary>Anexo (archivo) asociado a un ítem de antecedente/producción del CV.</summary>
public sealed class CvAntecedenteItemAdjunto
{
    public Guid Id { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid AdjuntoId { get; private set; }
    public DateTime FechaCarga { get; private set; }

    private CvAntecedenteItemAdjunto() { }

    public CvAntecedenteItemAdjunto(Guid itemId, Guid adjuntoId)
    {
        Id = Guid.NewGuid();
        ItemId = itemId;
        AdjuntoId = adjuntoId;
        FechaCarga = DateTime.UtcNow;
    }
}
