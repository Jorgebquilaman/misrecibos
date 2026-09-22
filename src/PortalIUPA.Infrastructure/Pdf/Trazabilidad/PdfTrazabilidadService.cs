#pragma warning disable CA1416
using System.Security.Cryptography;
using System.Text.Json;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using ZXing;

namespace PortalIUPA.Infrastructure.Pdf.Trazabilidad;

public interface IPdfTrazabilidadService
{
    Task<TrazabilidadResultado> AddTraceabilityAsync(
        string inputPdf,
        string? outputPdf = null,
        string userId = "",
        string? traceId = null,
        TrazabilidadPosicion position = TrazabilidadPosicion.FooterRight,
        TrazabilidadCodificacion encoding = TrazabilidadCodificacion.Morse,
        DateTime? fechaHora = null,
        string? registroJsonPath = null,
        CancellationToken ct = default);

    Task<byte[]> AddTraceabilityToBytesAsync(
        byte[] pdfBytes,
        string inputFileName,
        string userId,
        string? traceId = null,
        TrazabilidadPosicion position = TrazabilidadPosicion.FooterRight,
        TrazabilidadCodificacion encoding = TrazabilidadCodificacion.Morse,
        DateTime? fechaHora = null,
        string? registroJsonPath = null,
        CancellationToken ct = default);

    Task<TrazabilidadDecodificacionResultado?> TryDecodeAsync(string inputPdfOrImage, CancellationToken ct = default);
}

public sealed class PdfTrazabilidadService : IPdfTrazabilidadService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public async Task<TrazabilidadResultado> AddTraceabilityAsync(
        string inputPdf,
        string? outputPdf = null,
        string userId = "",
        string? traceId = null,
        TrazabilidadPosicion position = TrazabilidadPosicion.FooterRight,
        TrazabilidadCodificacion encoding = TrazabilidadCodificacion.Morse,
        DateTime? fechaHora = null,
        string? registroJsonPath = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(inputPdf)) throw new ArgumentException("inputPdf requerido", nameof(inputPdf));
        if (!File.Exists(inputPdf)) throw new FileNotFoundException($"No se encontró el PDF de entrada: {inputPdf}", inputPdf);
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("userId requerido", nameof(userId));

        var fecha = fechaHora ?? DateTime.Now;
        var id = string.IsNullOrWhiteSpace(traceId) ? GenerarTraceId() : NormalizarTraceId(traceId!);
        var hash = await CalcularSha256Async(inputPdf, ct);

        var inputNombre = Path.GetFileName(inputPdf);
        var output = outputPdf;
        if (string.IsNullOrWhiteSpace(output))
        {
            var dir = Path.GetDirectoryName(inputPdf) ?? ".";
            var baseName = Path.GetFileNameWithoutExtension(inputPdf);
            output = Path.Combine(dir, $"{baseName}-trazabilidad.pdf");
        }

        // Modo Modify: el overlay se agrega sobre el documento original sin re-importar páginas,
        // preservando texto seleccionable, vectores, imágenes y anotaciones internas (links).
        using var fsIn = File.OpenRead(inputPdf);
        PdfDocument doc;
        try
        {
            doc = PdfReader.Open(fsIn, PdfDocumentOpenMode.Modify);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"No se pudo abrir el PDF de entrada: {ex.Message}", ex);
        }

        // Preparar contenido de trazabilidad (solo traceId, opaco)
        string morse = string.Empty;
        byte[]? barcodePng = null;
        if (encoding == TrazabilidadCodificacion.Morse)
        {
            morse = MorseEncoder.Encode(id);
        }
        else
        {
            barcodePng = BarcodeEncoder.GenerarPngBytes(id);
        }

        // Dibujar overlay en cada página
        for (int i = 0; i < doc.PageCount; i++)
        {
            var page = doc.Pages[i];
            // PdfSharpCore: page.Width/Height ya están en puntos; respetar orientación
            var w = page.Width.Point;
            var h = page.Height.Point;

            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            // Capa adicional, discreta, con transparencia ligera
            var brush = XBrushes.Gray;
            // Para morse usamos gris 60% para discreción; para barcode negro 100% pero pequeño

            if (encoding == TrazabilidadCodificacion.Morse)
            {
                var morseWidth = MorseEncoder.CalcularAncho(morse);
                const double morseHeight = 1.2; // alto de punto/raya del MorseEncoder
                var (x, y) = CalcularCoordenadas(w, h, morseWidth, morseHeight, position);
                var morseBrush = new XSolidBrush(XColor.FromArgb(255, 0, 0, 0));
                MorseEncoder.Dibujar(gfx, x, y, morse, morseBrush);
            }
            else
            {
                const double bw = 70;
                const double bh = 14;
                var (x, y) = CalcularCoordenadas(w, h, bw, bh, position);
                // Dibujar barcode como XImage escalada a 70x14 pt (usar Func<Stream> para que PdfSharpCore no dependa de stream cerrado)
                var img = XImage.FromStream(() => new MemoryStream(barcodePng!));
                gfx.DrawImage(img, x, y, bw, bh);
            }
        }

        // Guardar nuevo PDF (original intacto)
        var outDir = Path.GetDirectoryName(output);
        if (!string.IsNullOrWhiteSpace(outDir) && !Directory.Exists(outDir))
            Directory.CreateDirectory(outDir);
        doc.Save(output);

        // Hash del PDF marcado (permite resolver por hash aunque el usuario lo renombre)
        string outputHash;
        using (var fsOut = File.OpenRead(output))
        using (var shaOut = SHA256.Create())
            outputHash = Convert.ToHexString(shaOut.ComputeHash(fsOut)).ToLowerInvariant();

        // Registro JSON
        var registroPath = registroJsonPath;
        if (string.IsNullOrWhiteSpace(registroPath))
        {
            var outDir2 = Path.GetDirectoryName(output) ?? ".";
            registroPath = Path.Combine(outDir2, "trace-registry.json");
        }
        var registro = new TrazabilidadRegistro(
            TraceId: id,
            UserId: userId,
            InputFile: inputNombre,
            InputHashSha256: hash,
            FechaHora: fecha,
            Encoding: encoding.ToString().ToLowerInvariant(),
            Position: TrazabilidadPosicionParser.ToDisplay(position),
            OutputFile: Path.GetFileName(output),
            OutputHashSha256: outputHash);
        await RegistrarAsync(registroPath, registro, ct);

        return new TrazabilidadResultado(
            TraceId: id,
            InputPdf: inputPdf,
            OutputPdf: output,
            InputHashSha256: hash,
            UserId: userId,
            FechaHora: fecha,
            Encoding: encoding,
            Position: position);
    }

    public async Task<byte[]> AddTraceabilityToBytesAsync(
        byte[] pdfBytes,
        string inputFileName,
        string userId,
        string? traceId = null,
        TrazabilidadPosicion position = TrazabilidadPosicion.FooterRight,
        TrazabilidadCodificacion encoding = TrazabilidadCodificacion.Morse,
        DateTime? fechaHora = null,
        string? registroJsonPath = null,
        CancellationToken ct = default)
    {
        if (pdfBytes == null || pdfBytes.Length == 0) throw new ArgumentException("pdfBytes vacío", nameof(pdfBytes));
        if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("userId requerido", nameof(userId));

        var fecha = fechaHora ?? DateTime.Now;
        var id = string.IsNullOrWhiteSpace(traceId) ? GenerarTraceId() : NormalizarTraceId(traceId!);
        var hash = CalcularSha256(pdfBytes);

        string morse = string.Empty;
        byte[]? barcodePng = null;
        if (encoding == TrazabilidadCodificacion.Morse)
            morse = MorseEncoder.Encode(id);
        else
            barcodePng = BarcodeEncoder.GenerarPngBytes(id);

        // Modo Modify: el overlay se agrega sobre el documento original sin re-importar páginas.
        // Esto preserva anotaciones internas (links del índice a los anexos) que un Import+AddPage rompería.
        var msIn = new MemoryStream(pdfBytes);
        PdfDocument doc;
        try
        {
            doc = PdfReader.Open(msIn, PdfDocumentOpenMode.Modify);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"No se pudo abrir el PDF de entrada: {ex.Message}", ex);
        }

        for (int i = 0; i < doc.PageCount; i++)
        {
            var page = doc.Pages[i];
            var w = page.Width.Point;
            var h = page.Height.Point;
            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            if (encoding == TrazabilidadCodificacion.Morse)
            {
                var morseWidth = MorseEncoder.CalcularAncho(morse);
                const double morseHeight = 1.2; // alto de punto/raya del MorseEncoder
                var (x, y) = CalcularCoordenadas(w, h, morseWidth, morseHeight, position);
                var morseBrush = new XSolidBrush(XColor.FromArgb(255, 0, 0, 0));
                MorseEncoder.Dibujar(gfx, x, y, morse, morseBrush);
            }
            else
            {
                const double bw = 70;
                const double bh = 14;
                var (x, y) = CalcularCoordenadas(w, h, bw, bh, position);
                var img = XImage.FromStream(() => new MemoryStream(barcodePng!));
                gfx.DrawImage(img, x, y, bw, bh);
            }
        }

        using var msOut = new MemoryStream();
        doc.Save(msOut, false);
        var resultBytes = msOut.ToArray();

        // Registro JSON (si se provee ruta, sino junto al inputFileName en temp)
        var regPath = registroJsonPath;
        if (string.IsNullOrWhiteSpace(regPath))
        {
            var tmpDir = Path.GetTempPath();
            regPath = Path.Combine(tmpDir, "trace-registry.json");
            // También intentar junto al archivo de salida si se conoce
            try
            {
                var outDir = Path.GetDirectoryName(inputFileName);
                if (!string.IsNullOrWhiteSpace(outDir) && Directory.Exists(outDir))
                    regPath = Path.Combine(outDir, "trace-registry.json");
            }
            catch { }
        }
        var registro = new TrazabilidadRegistro(
            TraceId: id,
            UserId: userId,
            InputFile: inputFileName,
            InputHashSha256: hash,
            FechaHora: fecha,
            Encoding: encoding.ToString().ToLowerInvariant(),
            Position: TrazabilidadPosicionParser.ToDisplay(position),
            OutputFile: Path.GetFileNameWithoutExtension(inputFileName) + "-trazabilidad.pdf",
            OutputHashSha256: CalcularSha256(resultBytes));
        await RegistrarAsync(regPath, registro, ct);

        return resultBytes;
    }

    private static string CalcularSha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task<TrazabilidadDecodificacionResultado?> TryDecodeAsync(string inputPdfOrImage, CancellationToken ct = default)
    {
        if (!File.Exists(inputPdfOrImage)) throw new FileNotFoundException($"No se encontró el archivo: {inputPdfOrImage}", inputPdfOrImage);
        var ext = Path.GetExtension(inputPdfOrImage).ToLowerInvariant();
        if (ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".tiff")
        {
            return await TryDecodeImageAsync(inputPdfOrImage, ct);
        }
        // Asumir PDF: intentar extraer barcode de cada página renderizada como imagen
        // Dado que PdfSharpCore no rasteriza, intentamos:
        // 1) Buscar en el registro JSON cercano si existe
        // 2) Intentar decodificar barcode extrayendo imágenes embebidas del PDF
        // Para morse vectorial puro, la decodificación automática desde PDF requiere análisis de contenido vectorial;
        // se expone vía registro y MorseEncoder.Decode si se recupera la cadena morse manualmente.

        // Intentar extraer imágenes del PDF y decodificar
        try
        {
            var doc = PdfReader.Open(inputPdfOrImage, PdfDocumentOpenMode.Import);
            foreach (PdfPage page in doc.Pages)
            {
                // PdfSharpCore no expone fácilmente imágenes; como fallback, intentamos leer el PDF como binario y buscar barcode
                // En la práctica, el flujo de decodificación recomendado es: renderizar la página a imagen (usando herramienta externa)
                // y luego decodificar. Aquí implementamos decodificación vía registro si está disponible.
            }
        }
        catch { }

        // Fallback: buscar registro JSON en el mismo directorio
        var dir = Path.GetDirectoryName(inputPdfOrImage) ?? ".";
        var regPath = Path.Combine(dir, "trace-registry.json");
        if (File.Exists(regPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(regPath, ct);
                var registros = JsonSerializer.Deserialize<List<TrazabilidadRegistro>>(json, JsonOpts);
                var nombre = Path.GetFileName(inputPdfOrImage);
                var match = registros?.FirstOrDefault(r => r.OutputFile == nombre || r.InputFile == nombre);
                if (match != null)
                {
                    var enc = match.Encoding.Equals("barcode", StringComparison.OrdinalIgnoreCase)
                        ? TrazabilidadCodificacion.Barcode : TrazabilidadCodificacion.Morse;
                    return new TrazabilidadDecodificacionResultado(match.TraceId, enc, regPath);
                }
            }
            catch { }
        }

        // Intentar decodificar como si el PDF fuera en realidad una imagen (escaneo)
        // Convertir primera página a imagen requeriría Pdfium; como no está disponible, retornamos null para indicar que se requiere inspección visual/registro
        return null;
    }

    private async Task<TrazabilidadDecodificacionResultado?> TryDecodeImageAsync(string imagePath, CancellationToken ct)
    {
        try
        {
            // Decodificación administrada de PNG (sin System.Drawing); incluye fallback interno
            var bytes = await File.ReadAllBytesAsync(imagePath, ct);
            var text = BarcodeEncoder.Decodificar(bytes);
            if (!string.IsNullOrWhiteSpace(text) && text.StartsWith("DOC-"))
                return new TrazabilidadDecodificacionResultado(text, TrazabilidadCodificacion.Barcode, imagePath);
        }
        catch { }

        // Para morse en imagen escaneada, se requeriría procesamiento de imagen (detección de puntos/rayas)
        // Se deja como punto de extensión; por ahora se resuelve vía registro JSON
        var dir = Path.GetDirectoryName(imagePath) ?? ".";
        var regPath = Path.Combine(dir, "trace-registry.json");
        if (File.Exists(regPath))
        {
            var json = await File.ReadAllTextAsync(regPath, ct);
            var registros = JsonSerializer.Deserialize<List<TrazabilidadRegistro>>(json, JsonOpts);
            // Si la imagen es un escaneo del PDF marcado, el nombre original puede estar en InputFile
            var nombre = Path.GetFileNameWithoutExtension(imagePath);
            var match = registros?.FirstOrDefault(r => r.TraceId.Contains(nombre) || nombre.Contains(r.TraceId));
            if (match != null)
            {
                var enc = match.Encoding.Equals("barcode", StringComparison.OrdinalIgnoreCase)
                    ? TrazabilidadCodificacion.Barcode : TrazabilidadCodificacion.Morse;
                return new TrazabilidadDecodificacionResultado(match.TraceId, enc, regPath);
            }
        }
        return null;
    }

    private static string GenerarTraceId()
    {
        Span<byte> bytes = stackalloc byte[4];
        RandomNumberGenerator.Fill(bytes);
        var hex = Convert.ToHexString(bytes);
        return $"DOC-{hex}";
    }

    private static string NormalizarTraceId(string traceId)
    {
        var t = traceId.Trim().ToUpperInvariant();
        if (!t.StartsWith("DOC-")) t = $"DOC-{t}";
        // Validar que después de DOC- sean hex
        var suffix = t.Substring(4);
        if (suffix.Length < 4 || suffix.Length > 16) throw new ArgumentException("traceId debe tener entre 4 y 16 caracteres hex después de DOC-");
        if (!suffix.All(c => Uri.IsHexDigit(c))) throw new ArgumentException("traceId solo admite hex [0-9A-F]");
        return t;
    }

    private static async Task<string> CalcularSha256Async(string filePath, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hash = await sha.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task RegistrarAsync(string registroPath, TrazabilidadRegistro registro, CancellationToken ct)
    {
        List<TrazabilidadRegistro> lista;
        if (File.Exists(registroPath))
        {
            try
            {
                var jsonExistente = await File.ReadAllTextAsync(registroPath, ct);
                lista = JsonSerializer.Deserialize<List<TrazabilidadRegistro>>(jsonExistente, JsonOpts) ?? new();
            }
            catch
            {
                lista = new();
            }
        }
        else
        {
            lista = new();
        }
        lista.Add(registro);
        var json = JsonSerializer.Serialize(lista, JsonOpts);
        var dir = Path.GetDirectoryName(registroPath);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(registroPath, json, ct);
    }

    private static (double x, double y) CalcularCoordenadas(double pageW, double pageH, double contentW, double contentH, TrazabilidadPosicion pos)
    {
        const double marginX = 20;
        const double marginY = 14;
        // XGraphics usa origen top-left (y crece hacia abajo):
        // Header: y chico (cerca del borde superior). Footer: y grande (cerca del borde inferior).
        double x = pos switch
        {
            TrazabilidadPosicion.HeaderLeft or TrazabilidadPosicion.FooterLeft => marginX,
            TrazabilidadPosicion.HeaderCenter or TrazabilidadPosicion.FooterCenter => (pageW - contentW) / 2,
            TrazabilidadPosicion.HeaderRight or TrazabilidadPosicion.FooterRight => pageW - contentW - marginX,
            _ => pageW - contentW - marginX
        };
        double y = pos switch
        {
            TrazabilidadPosicion.HeaderLeft or TrazabilidadPosicion.HeaderCenter or TrazabilidadPosicion.HeaderRight
                => marginY,
            _ => pageH - marginY - contentH
        };
        // Clamp para páginas pequeñas
        x = Math.Clamp(x, marginX, pageW - contentW - marginX);
        y = Math.Clamp(y, marginY, pageH - contentH - marginY);
        return (x, y);
    }
}
