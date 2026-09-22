#nullable enable
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using ZXing;
using ZXing.Common;

namespace PortalIUPA.Infrastructure.Pdf.Trazabilidad;

/// <summary>
/// Generación y decodificación de Code128 100% administrada (sin System.Drawing / libgdiplus):
/// la generación usa ZXing BitMatrix → PNG grayscale 8-bit propio; la decodificación lee PNG
/// (grayscale/RGB 8-bit, todos los filtros) → ZXing. Fallback opcional a System.Drawing si existe.
/// </summary>
public static class BarcodeEncoder
{
    /// <summary>Genera el Code128 del traceId como PNG grayscale 8-bit (300 dpi nominal).</summary>
    public static byte[] GenerarPngBytes(string traceId, int widthPx = 210, int heightPx = 42)
    {
        if (string.IsNullOrWhiteSpace(traceId)) throw new ArgumentException("traceId vacío", nameof(traceId));
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.CODE_128,
            Options = new EncodingOptions
            {
                Width = widthPx,
                Height = heightPx,
                Margin = 1,
                PureBarcode = true
            }
        };
        BitMatrix matrix = writer.Encode(traceId);
        return PngWriter.EscribirGris(matrix.Width, matrix.Height,
            (x, y) => matrix[x, y] ? (byte)0 : (byte)255);
    }

    /// <summary>Decodifica un Code128 desde bytes PNG. Primero intenta el lector PNG administrado;
    /// si no aplica, cae al fallback con System.Drawing (solo plataformas que lo soporten).</summary>
    public static string? Decodificar(byte[] pngBytes)
    {
        try
        {
            if (PngReader.IntentarLeerLuminancia(pngBytes, out var lum, out var w, out var h))
            {
                var resultado = CrearReader().Decode(new RGBLuminanceSource(lum, w, h, RGBLuminanceSource.BitmapFormat.Gray8));
                if (!string.IsNullOrWhiteSpace(resultado?.Text))
                    return resultado.Text;
            }
        }
        catch { }

        try
        {
            using var ms = new MemoryStream(pngBytes);
            using var bmp = new System.Drawing.Bitmap(ms);
            return Decodificar(bmp);
        }
        catch { }
        return null;
    }

    /// <summary>Decodifica un Code128 desde un Bitmap (requiere System.Drawing / libgdiplus).</summary>
#pragma warning disable CA1416
    public static string? Decodificar(System.Drawing.Bitmap bitmap)
    {
        try
        {
            var reader = new BarcodeReader<System.Drawing.Bitmap>(b => new BitmapLuminanceSource(b))
            {
                Options = new DecodingOptions
                {
                    PossibleFormats = new[] { BarcodeFormat.CODE_128 },
                    TryHarder = true
                }
            };
            return reader.Decode(bitmap)?.Text;
        }
        catch
        {
            return null;
        }
    }
#pragma warning restore CA1416

    private static BarcodeReader<RGBLuminanceSource> CrearReader() => new(l => l)
    {
        Options = new DecodingOptions
        {
            PossibleFormats = new[] { BarcodeFormat.CODE_128 },
            TryHarder = true
        }
    };

#pragma warning disable CA1416
    private sealed class BitmapLuminanceSource : RGBLuminanceSource
    {
        public BitmapLuminanceSource(System.Drawing.Bitmap bitmap)
            : base(CalcularLuminancia(bitmap), bitmap.Width, bitmap.Height)
        {
        }

        private static byte[] CalcularLuminancia(System.Drawing.Bitmap bitmap)
        {
            var data = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                var bytes = new byte[data.Stride * bitmap.Height];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
                var luminances = new byte[bitmap.Width * bitmap.Height];
                for (int y = 0; y < bitmap.Height; y++)
                {
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        int offset = y * data.Stride + x * 4;
                        byte b = bytes[offset];
                        byte g = bytes[offset + 1];
                        byte r = bytes[offset + 2];
                        int lum = (306 * r + 601 * g + 117 * b) >> 10;
                        luminances[y * bitmap.Width + x] = (byte)lum;
                    }
                }
                return luminances;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private BitmapLuminanceSource(byte[] luminances, int width, int height)
            : base(luminances, width, height, RGBLuminanceSource.BitmapFormat.Gray8)
        {
        }
    }
#pragma warning restore CA1416

    /// <summary>Escritor PNG mínimo: grayscale 8-bit, filtro None, un solo IDAT (zlib).</summary>
    internal static class PngWriter
    {
        public static byte[] EscribirGris(int width, int height, Func<int, int, byte> pixel)
        {
            var raw = new byte[(width + 1) * height];
            for (int y = 0; y < height; y++)
            {
                int fila = y * (width + 1);
                raw[fila] = 0; // filtro None
                for (int x = 0; x < width; x++)
                    raw[fila + 1 + x] = pixel(x, y);
            }

            using var ms = new MemoryStream();
            ms.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

            var ihdr = new byte[13];
            BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0), (uint)width);
            BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4), (uint)height);
            ihdr[8] = 8;  // bit depth
            ihdr[9] = 0;  // color type: grayscale
            // [10] compression=0, [11] filter=0, [12] interlace=0
            EscribirChunk(ms, "IHDR", ihdr);

            byte[] idat;
            using (var comp = new MemoryStream())
            {
                comp.WriteByte(0x78); comp.WriteByte(0x9C); // cabecera zlib (deflate default)
                using (var def = new DeflateStream(comp, CompressionLevel.Fastest, leaveOpen: true))
                    def.Write(raw, 0, raw.Length);
                Span<byte> adler = stackalloc byte[4];
                BinaryPrimitives.WriteUInt32BigEndian(adler, Adler32(raw));
                comp.Write(adler);
                idat = comp.ToArray();
            }
            EscribirChunk(ms, "IDAT", idat);
            EscribirChunk(ms, "IEND", Array.Empty<byte>());
            return ms.ToArray();
        }

        private static void EscribirChunk(Stream s, string tipo, byte[] datos)
        {
            Span<byte> cab = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(cab, (uint)datos.Length);
            s.Write(cab);
            var tipoBytes = Encoding.ASCII.GetBytes(tipo);
            s.Write(tipoBytes);
            s.Write(datos);
            BinaryPrimitives.WriteUInt32BigEndian(cab, Crc32(tipoBytes, datos));
            s.Write(cab);
        }

        private static uint Crc32(byte[] tipo, byte[] datos)
        {
            uint crc = 0xFFFFFFFF;
            foreach (var b in tipo) crc = PasoCrc(crc, b);
            foreach (var b in datos) crc = PasoCrc(crc, b);
            return crc ^ 0xFFFFFFFF;
        }

        private static uint PasoCrc(uint crc, byte b)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            return crc;
        }

        private static uint Adler32(byte[] datos)
        {
            uint a = 1, b = 0;
            foreach (var x in datos)
            {
                a = (a + x) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }
    }

    /// <summary>Lector PNG mínimo: 8-bit grayscale (0) o RGB (2), no entrelazado, todos los filtros.</summary>
    internal static class PngReader
    {
        private static readonly byte[] Firma = { 137, 80, 78, 71, 13, 10, 26, 10 };

        public static bool IntentarLeerLuminancia(byte[] png, out byte[] luminancia, out int width, out int height)
        {
            luminancia = Array.Empty<byte>();
            width = height = 0;
            if (png.Length < 8 || !png.AsSpan(0, 8).SequenceEqual(Firma)) return false;

            int pos = 8;
            int w = 0, h = 0, bitDepth = 0, colorType = 0, interlace = 0;
            using var idat = new MemoryStream();
            while (pos + 12 <= png.Length)
            {
                uint len = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(pos));
                var tipo = Encoding.ASCII.GetString(png, pos + 4, 4);
                int datosPos = pos + 8;
                if (datosPos + (int)len > png.Length) return false;
                if (tipo == "IHDR")
                {
                    w = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(datosPos));
                    h = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(datosPos + 4));
                    bitDepth = png[datosPos + 8];
                    colorType = png[datosPos + 9];
                    interlace = png[datosPos + 12];
                }
                else if (tipo == "IDAT")
                {
                    idat.Write(png, datosPos, (int)len);
                }
                else if (tipo == "IEND")
                {
                    break;
                }
                pos = datosPos + (int)len + 4; // + CRC
            }

            if (w <= 0 || h <= 0 || bitDepth != 8 || interlace != 0 || (colorType != 0 && colorType != 2))
                return false;

            // Inflar zlib (saltar 2 bytes de cabecera zlib; el adler final se ignora)
            byte[] raw;
            var idatBytes = idat.ToArray();
            if (idatBytes.Length < 2) return false;
            using (var inf = new DeflateStream(new MemoryStream(idatBytes, 2, idatBytes.Length - 2), CompressionMode.Decompress))
            using (var salida = new MemoryStream())
            {
                inf.CopyTo(salida);
                raw = salida.ToArray();
            }

            int bpp = colorType == 0 ? 1 : 3;
            int stride = w * bpp;
            if (raw.Length < (stride + 1) * h) return false;

            var datos = new byte[stride * h];
            int posRaw = 0;
            for (int y = 0; y < h; y++)
            {
                int filtro = raw[posRaw++];
                int baseFila = y * stride;
                int basePrev = (y - 1) * stride;
                for (int x = 0; x < stride; x++)
                {
                    int val = raw[posRaw++];
                    int a = x >= bpp ? datos[baseFila + x - bpp] : 0;
                    int b = y > 0 ? datos[basePrev + x] : 0;
                    int c = (x >= bpp && y > 0) ? datos[basePrev + x - bpp] : 0;
                    int recon = filtro switch
                    {
                        0 => val,
                        1 => val + a,
                        2 => val + b,
                        3 => val + (a + b) / 2,
                        4 => val + Paeth(a, b, c),
                        _ => val
                    };
                    datos[baseFila + x] = (byte)(recon & 0xFF);
                }
            }

            luminancia = new byte[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (bpp == 1)
                    {
                        luminancia[y * w + x] = datos[y * stride + x];
                    }
                    else
                    {
                        int off = y * stride + x * 3;
                        int lum = (306 * datos[off] + 601 * datos[off + 1] + 117 * datos[off + 2]) >> 10;
                        luminancia[y * w + x] = (byte)lum;
                    }
                }
            }
            width = w;
            height = h;
            return true;
        }

        private static int Paeth(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }
    }
}
