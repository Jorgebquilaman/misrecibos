namespace PortalIUPA.Worker.Reloj.Models;

/// <summary>
/// Un registro de asistencia crudo leído del dispositivo, ya traducido a dominio.
/// </summary>
public sealed record AttendanceRecord
{
    /// <summary>Número de legajo / enrollment (Pin del usuario en el dispositivo).</summary>
    public string Pin { get; init; } = string.Empty;

    public DateTime Timestamp { get; init; }

    /// <summary>Byte de estado del registro tal como lo reporta el equipo.</summary>
    public byte Estado { get; init; }

    /// <summary>Byte "punch"/verify del registro (modo de verificación: 0 clave, 1 huella, 2 tarjeta…).</summary>
    public byte ModoVerificacion { get; init; }

    /// <summary>Tipo de marca derivado del estado (convenio del software del proveedor: 1 = salida).</summary>
    public bool EsSalida => Estado == 1;
}
