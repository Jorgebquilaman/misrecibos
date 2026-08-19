namespace PortalIUPA.Domain.Entities;

/// <summary>Registro de auditoría inmutable de accesos (migra Log_Accesos del legacy). Append-only.</summary>
public sealed class AccesoLog
{
    public Guid Id { get; private set; }
    public Guid? EmpleadoId { get; private set; }
    public string Correo { get; private set; } = null!;
    public string Accion { get; private set; } = null!;
    public string? Ip { get; private set; }
    public string? Dispositivo { get; private set; }
    public DateTime FechaHora { get; private set; }

    private AccesoLog() { }

    public AccesoLog(string correo, string accion, string? ip = null, string? dispositivo = null, Guid? empleadoId = null)
    {
        if (string.IsNullOrWhiteSpace(correo)) throw new ArgumentException("El correo es obligatorio.", nameof(correo));
        if (string.IsNullOrWhiteSpace(accion)) throw new ArgumentException("La acción es obligatoria.", nameof(accion));

        Id = Guid.NewGuid();
        Correo = correo;
        Accion = accion;
        Ip = ip;
        Dispositivo = dispositivo;
        EmpleadoId = empleadoId;
        FechaHora = DateTime.UtcNow;
    }
}