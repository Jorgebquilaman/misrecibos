using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalIUPA.Infrastructure.Pdf.Trazabilidad;

namespace PortalIUPA.Api.Controllers;

/// <summary>
/// Consulta de trazabilidad de PDFs para administradores.
/// Permite resolver la información de trazabilidad desde el traceId (código Morse)
/// visible en el PDF, o subiendo el propio PDF marcado.
/// </summary>
[Authorize(Policy = "Administrador")]
[Route("api/admin/trazabilidad")]
public sealed class TrazabilidadController : ApiControllerBase
{
    private readonly IPdfTrazabilidadService _trazabilidad;
    private readonly TrazabilidadRegistroRepository _registro;

    public TrazabilidadController(IPdfTrazabilidadService trazabilidad, TrazabilidadRegistroRepository registro)
    {
        _trazabilidad = trazabilidad;
        _registro = registro;
    }

    /// <summary>Resuelve la trazabilidad a partir de un traceId (DOC-XXXXXXXX) obtenido del código Morse del PDF.</summary>
    [HttpPost("consultar-traceid")]
    public async Task<IActionResult> ConsultarTraceId([FromBody] ConsultarTraceIdRequest request, CancellationToken ct)
    {
        var traceId = request.TraceId?.Trim();
        if (string.IsNullOrWhiteSpace(traceId))
            return BadRequest(new { error = "Debe indicar el traceId (DOC-XXXXXXXX) del PDF." });

        var registro = await _registro.ObtenerPorTraceIdAsync(traceId, ct);
        if (registro is null)
            return NotFound(new { error = $"No se encontró trazabilidad para el traceId '{traceId}'." });

        return Ok(registro);
    }

    /// <summary>
    /// Resuelve la trazabilidad decodificando el código Morse (puntos y rayas) visible en el PDF.
    /// </summary>
    [HttpPost("decodificar-morse")]
    public async Task<IActionResult> DecodificarMorse([FromBody] DecodificarMorseRequest request, CancellationToken ct)
    {
        var morse = (request.Morse ?? string.Empty).Replace("/", " ").Replace("|", " ").Replace("\t", " ");

        if (string.IsNullOrWhiteSpace(morse))
            return BadRequest(new { error = "Debe ingresar el código Morse (puntos y rayas) del PDF." });

        if (!morse.All(c => c is '.' or '-' or ' '))
            return BadRequest(new { error = "El código Morse solo admite puntos (.), rayas (-) y espacios (o /) entre letras." });

        try
        {
            var texto = MorseEncoder.Decode(morse);
            if (string.IsNullOrWhiteSpace(texto))
                return BadRequest(new { error = "No se pudo interpretar el código Morse ingresado." });

            // Normaliza/antepone DOC- si corresponde
            var registro = await _registro.ObtenerPorTraceIdAsync(texto, ct);
            if (registro is null)
                return NotFound(new { error = $"El código Morse corresponde a '{texto}', pero no se encontró trazabilidad en el registro." });

            return Ok(registro);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Sube un PDF (o su escaneo en imagen) ya marcado y devuelve su información de trazabilidad.</summary>
    [RequestSizeLimit(10 * 1024 * 1024)]
    [HttpPost("decodificar")]
    public async Task<IActionResult> Decodificar([FromForm] IFormFile archivo, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { error = "Debe adjuntar un archivo (PDF, JPG o PNG)." });

        var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (ext is not (".pdf" or ".jpg" or ".jpeg" or ".png"))
            return BadRequest(new { error = "Formato no válido. Se admiten PDF, JPG o PNG." });

        // Guardar a un directorio temporal para poder decodificar / hashear
        var tempDir = Path.Combine(Path.GetTempPath(), "trazabilidad-consulta");
        Directory.CreateDirectory(tempDir);
        var archivoGuardado = Path.Combine(tempDir, archivo.FileName.Replace(' ', '_'));

        await using (var input = archivo.OpenReadStream())
        await using (var fs = System.IO.File.Create(archivoGuardado))
            await input.CopyToAsync(fs, ct);

        try
        {
            var sha256 = await CalcularSha256Async(archivoGuardado, ct);

            // 1) Búsqueda directa en el registro por nombre y hash
            var match = await _registro.BuscarPorArchivoAsync(Path.GetFileName(archivoGuardado), sha256, ct);
            if (match is not null)
                return Ok(match);

            // 2) Fallback: decodificar barcode desde imagen escaneada
            if (ext is ".jpg" or ".jpeg" or ".png")
            {
                try
                {
                    var decodificado = await _trazabilidad.TryDecodeAsync(archivoGuardado, ct);
                    if (decodificado is not null)
                    {
                        var porTraceId = await _registro.ObtenerPorTraceIdAsync(decodificado.TraceId, ct);
                        if (porTraceId is not null)
                            return Ok(porTraceId);
                    }
                }
                catch { /* se reporta como no encontrado */ }
            }

            return NotFound(new
            {
                error = "No se pudo resolver la trazabilidad del archivo. Verificá que sea un PDF ya marcado con el código Morse o un escaneo con código de barras, y que figure en el registro."
            });
        }
        finally
        {
            try { System.IO.File.Delete(archivoGuardado); } catch { }
        }
    }

    /// <summary>Listado completo del historial de trazabilidad.</summary>
    [HttpGet("historial")]
    public async Task<IActionResult> Historial(CancellationToken ct)
    {
        var todos = await _registro.ObtenerTodosAsync(ct);
        var ordenados = todos.OrderByDescending(r => r.FechaHora).ToList();
        return Ok(ordenados);
    }

    private static async Task<string> CalcularSha256Async(string ruta, CancellationToken ct)
    {
        await using var stream = System.IO.File.OpenRead(ruta);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public sealed record ConsultarTraceIdRequest(string? TraceId);
public sealed record DecodificarMorseRequest(string? Morse);
