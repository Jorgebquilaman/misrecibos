namespace PortalIUPA.Domain.Entities;

/// <summary>Registro de una descarga de marcas desde un reloj ZKTeco.</summary>
public sealed class RelojZkDescarga
{
    public Guid Id { get; private set; }
    public Guid RelojZkId { get; private set; }
    public DateTime Fecha { get; private set; }
    public int Leidas { get; private set; }
    public int Nuevas { get; private set; }
    public int Duplicadas { get; private set; }
    public int LegajosDesconocidos { get; private set; }
    /// <summary>"Ok" o "Error".</summary>
    public string Estado { get; private set; } = null!;
    public string? Mensaje { get; private set; }
    public string? UsuarioCorreo { get; private set; }

    private RelojZkDescarga() { }

    public static RelojZkDescarga Exitosa(Guid relojZkId, DateTime fecha, int leidas, int nuevas, int duplicadas,
        int desconocidos, string? usuarioCorreo) =>
        new()
        {
            Id = Guid.NewGuid(),
            RelojZkId = relojZkId,
            Fecha = fecha,
            Leidas = leidas,
            Nuevas = nuevas,
            Duplicadas = duplicadas,
            LegajosDesconocidos = desconocidos,
            Estado = "Ok",
            UsuarioCorreo = usuarioCorreo
        };

    public static RelojZkDescarga Fallida(Guid relojZkId, DateTime fecha, string mensaje, string? usuarioCorreo) =>
        new()
        {
            Id = Guid.NewGuid(),
            RelojZkId = relojZkId,
            Fecha = fecha,
            Estado = "Error",
            Mensaje = mensaje,
            UsuarioCorreo = usuarioCorreo
        };
}
