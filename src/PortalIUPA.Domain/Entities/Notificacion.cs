using PortalIUPA.Domain.Enums;

namespace PortalIUPA.Domain.Entities;

/// <summary>Notificación in-app: cambios de estado de licencias, anuncios urgentes, sistema.</summary>
public sealed class Notificacion
{
    public Guid Id { get; private set; }
    public Guid EmpleadoId { get; private set; }
    public TipoNotificacion Tipo { get; private set; }
    public string Titulo { get; private set; } = null!;
    public string Cuerpo { get; private set; } = null!;
    public string? Link { get; private set; }
    public bool Leida { get; private set; }
    public DateTime FechaCreacion { get; private set; }

    private Notificacion() { }

    public Notificacion(Guid empleadoId, TipoNotificacion tipo, string titulo, string cuerpo, string? link = null)
    {
        if (string.IsNullOrWhiteSpace(titulo)) throw new ArgumentException("El título es obligatorio.", nameof(titulo));

        Id = Guid.NewGuid();
        EmpleadoId = empleadoId;
        Tipo = tipo;
        Titulo = titulo;
        Cuerpo = cuerpo;
        Link = link;
        Leida = false;
        FechaCreacion = DateTime.UtcNow;
    }

    public void MarcarLeida() => Leida = true;
}