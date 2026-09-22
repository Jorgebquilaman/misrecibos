namespace PortalIUPA.Domain.Entities;

/// <summary>
/// Permiso de visualización de un reporte: por correo de usuario o por rol.
/// </summary>
public sealed class ReportePermiso
{
    public Guid Id { get; private set; }
    public Guid ReporteId { get; private set; }
    public string? Email { get; private set; }
    public string? Rol { get; private set; }

    private ReportePermiso() { }

    public ReportePermiso(Guid reporteId, string? email, string? rol)
    {
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(rol))
            throw new ArgumentException("El permiso requiere un correo o un rol.");
        Id = Guid.NewGuid();
        ReporteId = reporteId;
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        Rol = string.IsNullOrWhiteSpace(rol) ? null : rol.Trim();
    }
}
