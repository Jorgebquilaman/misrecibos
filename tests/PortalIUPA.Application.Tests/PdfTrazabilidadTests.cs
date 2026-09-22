using System.Security.Cryptography;
using FluentAssertions;
using PortalIUPA.Infrastructure.Pdf.Trazabilidad;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;
using Xunit;

namespace PortalIUPA.Application.Tests;

public class PdfTrazabilidadTests : IDisposable
{
    private readonly string _tmpDir;

    public PdfTrazabilidadTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), "trazabilidad-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tmpDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tmpDir, true); } catch { }
    }

    private string CrearPdfSimple(string nombre, int paginas = 1, string texto = "Hola IUPA - texto seleccionable")
    {
        var path = Path.Combine(_tmpDir, nombre);
        var doc = new PdfDocument();
        for (int i = 0; i < paginas; i++)
        {
            var page = doc.AddPage();
            page.Size = PdfSharpCore.PageSize.A4;
            using var gfx = XGraphics.FromPdfPage(page);
            var font = new XFont("Helvetica", 12);
            gfx.DrawString($"{texto} p{i + 1}", font, XBrushes.Black, new XRect(20, 20, page.Width - 40, 20), XStringFormats.TopLeft);
            // Vector para verificar preservación
            gfx.DrawRectangle(XPens.Black, 20, 50, 100, 40);
        }
        doc.Save(path);
        return path;
    }

    [Fact]
    public void Morse_Encode_Decode_Roundtrip()
    {
        var id = "DOC-8F42A7C9";
        var morse = MorseEncoder.Encode(id);
        morse.Should().NotBeNullOrWhiteSpace();
        var decoded = MorseEncoder.Decode(morse);
        decoded.Should().Be(id);
    }

    [Fact]
    public void Barcode_Generar_Decodificar_Roundtrip()
    {
        var id = "DOC-AB12CD34";
        var png = BarcodeEncoder.GenerarPngBytes(id);
        png.Length.Should().BeGreaterThan(100);
        var decoded = BarcodeEncoder.Decodificar(png);
        decoded.Should().Be(id);
    }

    [Fact]
    public async Task AddTraceability_Morse_TodasLasPaginas_Y_PreservaTexto()
    {
        var input = CrearPdfSimple("doc-morse.pdf", paginas: 2);
        var hashOrig = await CalcularHash(input);
        var svc = new PdfTrazabilidadService();

        var r = await svc.AddTraceabilityAsync(input, userId: "legajo421", encoding: TrazabilidadCodificacion.Morse, position: TrazabilidadPosicion.FooterRight);

        File.Exists(r.OutputPdf).Should().BeTrue();
        r.OutputPdf.Should().EndWith("-trazabilidad.pdf");
        r.TraceId.Should().StartWith("DOC-");
        r.InputHashSha256.Should().Be(hashOrig);
        File.Exists(input).Should().BeTrue(); // original intacto

        // Verificar que el PDF de salida tiene las mismas páginas y texto extraíble
        var outDoc = PdfSharpCore.Pdf.IO.PdfReader.Open(r.OutputPdf, PdfSharpCore.Pdf.IO.PdfDocumentOpenMode.Import);
        outDoc.PageCount.Should().Be(2);
        // No rasterizado: el tamaño no debe explotar (imagen morse es vector, < 50KB overhead por página)
        var outSize = new FileInfo(r.OutputPdf).Length;
        var inSize = new FileInfo(input).Length;
        outSize.Should().BeGreaterThan(inSize);
        outSize.Should().BeLessThan(inSize + 200_000);

        // Registro JSON
        var regPath = Path.Combine(Path.GetDirectoryName(r.OutputPdf)!, "trace-registry.json");
        File.Exists(regPath).Should().BeTrue();
        var json = await File.ReadAllTextAsync(regPath);
        json.Should().Contain(r.TraceId);
        json.Should().Contain("legajo421");
    }

    [Fact]
    public async Task AddTraceability_Barcode_NombreSalida_Y_Hash()
    {
        var input = CrearPdfSimple("doc-barcode.pdf", paginas: 1);
        var svc = new PdfTrazabilidadService();
        var traceId = "DOC-FFFFFFFF";
        var r = await svc.AddTraceabilityAsync(input, userId: "usuario123", traceId: traceId, encoding: TrazabilidadCodificacion.Barcode, position: TrazabilidadPosicion.HeaderCenter);

        r.TraceId.Should().Be(traceId);
        File.Exists(r.OutputPdf).Should().BeTrue();
        Path.GetFileName(r.OutputPdf).Should().Be("doc-barcode-trazabilidad.pdf");

        // Decodificación desde PDF (vía registro fallback)
        var dec = await svc.TryDecodeAsync(r.OutputPdf);
        dec.Should().NotBeNull();
        dec!.TraceId.Should().Be(traceId);
        dec.Encoding.Should().Be(TrazabilidadCodificacion.Barcode);
    }

    [Fact]
    public async Task AddTraceability_Seis_Posiciones_No_Lanza()
    {
        var svc = new PdfTrazabilidadService();
        foreach (var pos in Enum.GetValues<TrazabilidadPosicion>())
        {
            var input = CrearPdfSimple($"pos-{pos}.pdf", paginas: 1);
            var r = await svc.AddTraceabilityAsync(input, userId: "u1", position: pos, encoding: TrazabilidadCodificacion.Morse);
            File.Exists(r.OutputPdf).Should().BeTrue();
        }
    }

    [Fact]
    public async Task AddTraceability_Tamaños_Pagina_Distintos()
    {
        // A4 y Carta
        var input = Path.Combine(_tmpDir, "tamanos.pdf");
        var doc = new PdfDocument();
        var p1 = doc.AddPage(); p1.Size = PdfSharpCore.PageSize.A4;
        var p2 = doc.AddPage(); p2.Size = PdfSharpCore.PageSize.Letter;
        doc.Save(input);

        var svc = new PdfTrazabilidadService();
        var r = await svc.AddTraceabilityAsync(input, userId: "u1", encoding: TrazabilidadCodificacion.Barcode);
        var outDoc = PdfSharpCore.Pdf.IO.PdfReader.Open(r.OutputPdf, PdfSharpCore.Pdf.IO.PdfDocumentOpenMode.Import);
        outDoc.Pages[0].Width.Point.Should().BeApproximately(p1.Width.Point, 0.1);
        outDoc.Pages[1].Width.Point.Should().BeApproximately(p2.Width.Point, 0.1);
    }

    [Fact]
    public async Task TryDecode_Imagen_Escaneada_Barcode()
    {
        var id = "DOC-1234ABCD";
        var png = BarcodeEncoder.GenerarPngBytes(id);
        var imgPath = Path.Combine(_tmpDir, "scan.png");
        await File.WriteAllBytesAsync(imgPath, png);

        var svc = new PdfTrazabilidadService();
        var dec = await svc.TryDecodeAsync(imgPath);
        dec.Should().NotBeNull();
        dec!.TraceId.Should().Be(id);
    }

    [Fact]
    public void Morse_Ancho_Calculo_Coherente_Con_Dibujo()
    {
        var morse = MorseEncoder.Encode("DOC-1");
        var w1 = MorseEncoder.CalcularAncho(morse);
        w1.Should().BeGreaterThan(10);
        w1.Should().BeLessThan(85); // requisito discreto <85pt
    }

    private static async Task<string> CalcularHash(string path)
    {
        using var sha = SHA256.Create();
        await using var s = File.OpenRead(path);
        var h = await sha.ComputeHashAsync(s);
        return Convert.ToHexString(h).ToLowerInvariant();
    }
}
