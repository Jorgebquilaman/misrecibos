namespace PortalIUPA.Domain.Entities;

/// <summary>Marca de "leído" de un anuncio por un empleado (para el feed del dashboard).</summary>
public sealed class AnuncioLeido
{
    public Guid Id { get; private set; }
    public Guid AnuncioId { get; private set; }
    public Guid EmpleadoId { get; private set; }
    public DateTime FechaLectura { get; private set; }

    private AnuncioLeido() { }

    public AnuncioLeido(Guid anuncioId, Guid empleadoId)
    {
        Id = Guid.NewGuid();
        AnuncioId = anuncioId;
        EmpleadoId = empleadoId;
        FechaLectura = DateTime.UtcNow;
    }
}