namespace ZkAttendanceService.Options;

public sealed class ZkDeviceOptions
{
    public string Host { get; set; } = "192.168.1.204";
    public int Port { get; set; } = 4370;

    /// <summary>
    /// Comm key / password del dispositivo, si tiene una configurada. Dejar null si
    /// el reloj no exige autenticación (caso más común en instalaciones sin cambiar
    /// la configuración de fábrica).
    /// </summary>
    public int? CommKey { get; set; }

    public int TimeoutMs { get; set; } = 8000;
    public int PollIntervalMinutes { get; set; } = 5;
    public string SqlConnectionString { get; set; } = string.Empty;
}
