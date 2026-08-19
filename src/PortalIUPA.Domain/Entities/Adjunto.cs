namespace PortalIUPA.Domain.Entities;

/// <summary>Archivo adjunto (certificado médico, imagen, PDF) asociado a una entidad (solicitud, certificado).</summary>
public sealed class Adjunto
{
    public Guid Id { get; private set; }
    public string NombreArchivo { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long TamañoBytes { get; private set; }
    public string StorageKey { get; private set; } = null!;
    public string EntidadTipo { get; private set; } = null!;
    public Guid EntidadId { get; private set; }
    public Guid EmpleadoId { get; private set; }

    private Adjunto() { }

    public Adjunto(string nombreArchivo, string contentType, long tamañoBytes, string storageKey, Guid empleadoId)
    {
        if (string.IsNullOrWhiteSpace(nombreArchivo)) throw new ArgumentException("El nombre es obligatorio.", nameof(nombreArchivo));
        if (string.IsNullOrWhiteSpace(storageKey)) throw new ArgumentException("La clave de almacenamiento es obligatoria.", nameof(storageKey));

        Id = Guid.NewGuid();
        NombreArchivo = nombreArchivo;
        ContentType = contentType;
        TamañoBytes = tamañoBytes;
        StorageKey = storageKey;
        EntidadTipo = "solicitud";
        EntidadId = Guid.Empty;
        EmpleadoId = empleadoId;
    }

    public void VincularAEntidad(string entidadTipo, Guid entidadId)
    {
        EntidadTipo = entidadTipo;
        EntidadId = entidadId;
    }
}