namespace PortalIUPA.Domain.Entities;

/// <summary>Reloj biométrico ZKTeco (ej. X628-C) para descargar marcas. Dos modos:
/// "directo" (protocolo ZK por TCP al equipo) o "mssql" (lee las marcas que el software
/// ZKBio ya descargó a la base del reloj; útil cuando ZKBio mantiene ocupado el equipo).</summary>
public sealed class RelojZk
{
    public const string ModoDirecto = "directo";
    public const string ModoMssql = "mssql";

    public Guid Id { get; private set; }
    public string Nombre { get; private set; } = null!;
    public string Ip { get; private set; } = null!;
    public int Puerto { get; private set; }
    /// <summary>Clave de comunicación del equipo (CommKey). 0 = sin clave.</summary>
    public int CommKey { get; private set; }
    public string Modo { get; private set; } = ModoDirecto;
    public bool Activo { get; private set; }
    public DateTime? UltimaDescarga { get; private set; }
    public int UltimaCantidad { get; private set; }

    private RelojZk() { }

    public RelojZk(string nombre, string ip, int puerto = 4370, int commKey = 0, bool activo = true, string modo = ModoDirecto)
    {
        Editar(nombre, ip, puerto, commKey, activo, modo);
        Id = Guid.NewGuid();
    }

    public void Editar(string nombre, string ip, int puerto, int commKey, bool activo, string modo = ModoDirecto)
    {
        if (string.IsNullOrWhiteSpace(nombre)) throw new ArgumentException("El nombre es obligatorio.", nameof(nombre));
        if (string.IsNullOrWhiteSpace(ip)) throw new ArgumentException("La IP es obligatoria.", nameof(ip));
        if (puerto is < 1 or > 65535) throw new ArgumentException("El puerto debe estar entre 1 y 65535.", nameof(puerto));
        if (commKey is < 0 or > 999999) throw new ArgumentException("La CommKey debe estar entre 0 y 999999.", nameof(commKey));
        var modoNormalizado = (modo ?? ModoDirecto).Trim().ToLowerInvariant();
        if (modoNormalizado is not (ModoDirecto or ModoMssql))
            throw new ArgumentException("El modo debe ser 'directo' o 'mssql'.", nameof(modo));

        Nombre = nombre.Trim();
        Ip = ip.Trim();
        Puerto = puerto;
        CommKey = commKey;
        Modo = modoNormalizado;
        Activo = activo;
    }

    public void RegistrarDescarga(DateTime fecha, int cantidad)
    {
        UltimaDescarga = fecha;
        UltimaCantidad = cantidad;
    }
}
