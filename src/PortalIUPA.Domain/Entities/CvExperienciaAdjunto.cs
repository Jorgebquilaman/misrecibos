namespace PortalIUPA.Domain.Entities;

/// <summary>Anexo (archivo) asociado a una experiencia laboral del CV.</summary>
public sealed class CvExperienciaAdjunto
{
    public Guid Id { get; private set; }
    public Guid ExperienciaId { get; private set; }
    public Guid AdjuntoId { get; private set; }
    public DateTime FechaCarga { get; private set; }

    private CvExperienciaAdjunto() { }

    public CvExperienciaAdjunto(Guid experienciaId, Guid adjuntoId)
    {
        Id = Guid.NewGuid();
        ExperienciaId = experienciaId;
        AdjuntoId = adjuntoId;
        FechaCarga = DateTime.UtcNow;
    }
}
