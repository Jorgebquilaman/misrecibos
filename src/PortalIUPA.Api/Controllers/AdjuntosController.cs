using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Api.Controllers;

[Authorize]
[Route("api/adjuntos")]
public sealed class AdjuntosController : ApiControllerBase
{
    private static readonly string[] ExtensionesPermitidas = { ".pdf", ".jpg", ".jpeg", ".png" };
    private const long MaxBytes = 10 * 1024 * 1024;

    private readonly IFileStoragePort _storage;
    private readonly IAdjuntoRepository _adjuntos;

    public AdjuntosController(IFileStoragePort storage, IAdjuntoRepository adjuntos)
    {
        _storage = storage;
        _adjuntos = adjuntos;
    }

    /// <summary>Sube un archivo (adjunto de licencia). Máximo 10 MB; solo pdf/jpg/png.</summary>
    [HttpPost]
    [RequestSizeLimit(MaxBytes + 1024)]
    public async Task<IActionResult> Subir(IFormFile archivo)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { error = "Debe adjuntar un archivo." });
        if (archivo.Length > MaxBytes)
            return BadRequest(new { error = "El archivo supera los 10 MB." });

        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (!ExtensionesPermitidas.Contains(extension))
            return BadRequest(new { error = "Solo se permiten archivos PDF, JPG o PNG." });

        var id = Guid.NewGuid();
        var storageKey = $"adjuntos/{id}{extension}";
        await using var stream = archivo.OpenReadStream();
        await _storage.GuardarAsync(storageKey, archivo.ContentType, stream);

        var adjunto = new Adjunto(archivo.FileName, archivo.ContentType, archivo.Length, storageKey, EmpleadoId);
        await _adjuntos.AddAsync(adjunto);

        return Created($"/api/adjuntos/{adjunto.Id}", new
        {
            adjunto.Id,
            adjunto.NombreArchivo,
            adjunto.TamañoBytes,
            adjunto.ContentType
        });
    }
}