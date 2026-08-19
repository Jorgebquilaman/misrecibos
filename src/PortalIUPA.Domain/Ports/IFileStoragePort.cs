namespace PortalIUPA.Domain.Ports;

/// <summary>Almacenamiento de archivos (adjuntos de licencias, PDFs de certificados).</summary>
public interface IFileStoragePort
{
    /// <returns>Clave de almacenamiento del archivo guardado.</returns>
    Task<string> GuardarAsync(string storageKey, string contentType, Stream contenido, CancellationToken ct = default);

    Task<Stream> AbrirAsync(string storageKey, CancellationToken ct = default);

    Task EliminarAsync(string storageKey, CancellationToken ct = default);
}