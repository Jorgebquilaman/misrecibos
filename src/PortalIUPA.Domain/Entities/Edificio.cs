namespace PortalIUPA.Domain.Entities;

/// <summary>Edificio/sede del instituto con coordenadas para geolocalizar marcas manuales.</summary>
public sealed class Edificio
{
    public Guid Id { get; private set; }
    public string Nombre { get; private set; } = null!;
    public double Latitud { get; private set; }
    public double Longitud { get; private set; }
    /// <summary>Radio de detección en metros (una marca a esta distancia o menos se asocia al edificio).</summary>
    public int RadioMetros { get; private set; }
    public bool Activo { get; private set; }

    private Edificio() { }

    public Edificio(string nombre, double latitud, double longitud, int radioMetros = 100, bool activo = true)
    {
        if (string.IsNullOrWhiteSpace(nombre)) throw new ArgumentException("El nombre es obligatorio.", nameof(nombre));
        if (radioMetros <= 0) throw new ArgumentException("El radio debe ser positivo.", nameof(radioMetros));

        Id = Guid.NewGuid();
        Nombre = nombre.Trim();
        Latitud = latitud;
        Longitud = longitud;
        RadioMetros = radioMetros;
        Activo = activo;
    }

    public void Editar(string nombre, double latitud, double longitud, int radioMetros, bool activo)
    {
        if (string.IsNullOrWhiteSpace(nombre)) throw new ArgumentException("El nombre es obligatorio.", nameof(nombre));
        if (radioMetros <= 0) throw new ArgumentException("El radio debe ser positivo.", nameof(radioMetros));
        Nombre = nombre.Trim();
        Latitud = latitud;
        Longitud = longitud;
        RadioMetros = radioMetros;
        Activo = activo;
    }
}
