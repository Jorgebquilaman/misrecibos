namespace PortalIUPA.Infrastructure.Pdf.Trazabilidad;

/// <summary>
/// Conversión de traceId a Morse ITU y dibujo vectorial discreto.
/// Solo se codifica el traceId opaco (ej. DOC-8F42A7C9), no datos legibles.
/// </summary>
public static class MorseEncoder
{
    private static readonly IReadOnlyDictionary<char, string> Mapa = new Dictionary<char, string>
    {
        ['A'] = ".-",   ['B'] = "-...", ['C'] = "-.-.", ['D'] = "-..",  ['E'] = ".",
        ['F'] = "..-.", ['G'] = "--.",  ['H'] = "....", ['I'] = "..",   ['J'] = ".---",
        ['K'] = "-.-",  ['L'] = ".-..", ['M'] = "--",   ['N'] = "-.",   ['O'] = "---",
        ['P'] = ".--.", ['Q'] = "--.-", ['R'] = ".-.",  ['S'] = "...",  ['T'] = "-",
        ['U'] = "..-",  ['V'] = "...-", ['W'] = ".--",  ['X'] = "-..-", ['Y'] = "-.--",
        ['Z'] = "--..",
        ['0'] = "-----", ['1'] = ".----", ['2'] = "..---", ['3'] = "...--", ['4'] = "....-",
        ['5'] = ".....", ['6'] = "-....", ['7'] = "--...", ['8'] = "---..", ['9'] = "----.",
        ['-'] = "-....-",
    };

    public static string Encode(string traceId)
    {
        if (string.IsNullOrWhiteSpace(traceId)) throw new ArgumentException("traceId vacío", nameof(traceId));
        var upper = traceId.ToUpperInvariant();
        var parts = new List<string>(upper.Length);
        foreach (var ch in upper)
        {
            if (Mapa.TryGetValue(ch, out var morse))
                parts.Add(morse);
            else
                throw new ArgumentException($"Carácter no soportado en morse: '{ch}' (solo A-Z 0-9 -)");
        }
        return string.Join(" ", parts);
    }

    public static string Decode(string morse)
    {
        var inv = Mapa.ToDictionary(kv => kv.Value, kv => kv.Key);
        var letters = morse.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var chars = new char[letters.Length];
        for (var i = 0; i < letters.Length; i++)
        {
            if (!inv.TryGetValue(letters[i], out var ch))
                throw new ArgumentException($"Secuencia morse desconocida: {letters[i]}");
            chars[i] = ch;
        }
        return new string(chars);
    }

    /// <summary>
    /// Dibuja el morse como vectores discretos. Retorna ancho total ocupado.
    /// </summary>
    // Dimensiones discretas (pt): punto Ø1.2, raya 3.6×1.2, gaps 0.9 / 2.6.
    // "DOC-1" ≈ 79pt (<85 requerido); un DOC-XXXXXXXX completo ≈ 185pt en header-left.
    private const double DotD = 1.2;
    private const double DashW = 3.6;
    private const double DashH = 1.2;
    private const double GapElemento = 0.9;
    private const double GapLetra = 2.6;

    public static double Dibujar(PdfSharpCore.Drawing.XGraphics gfx, double x, double y, string morse,
        PdfSharpCore.Drawing.XBrush brush)
    {
        const double dotD = DotD;
        const double dashW = DashW;
        const double dashH = DashH;
        const double gapElemento = GapElemento;
        const double gapLetra = GapLetra;

        var curX = x;
        foreach (var ch in morse)
        {
            if (ch == '.')
            {
                // Círculo relleno centrado verticalmente
                gfx.DrawEllipse(brush, curX, y, dotD, dotD);
                curX += dotD + gapElemento;
            }
            else if (ch == '-')
            {
                gfx.DrawRectangle(brush, curX, y, dashW, dashH);
                curX += dashW + gapElemento;
            }
            else if (ch == ' ')
            {
                // Gap inter-letra: ya sumamos gapElemento tras el último elemento, añadir extra
                curX += gapLetra - gapElemento;
            }
        }
        // Quitar último gapElemento añadido de más
        if (curX > x) curX -= gapElemento;
        return curX - x;
    }

    public static double CalcularAncho(string morse)
    {
        const double dotD = DotD;
        const double dashW = DashW;
        const double gapElemento = GapElemento;
        const double gapLetra = GapLetra;
        double w = 0;
        foreach (var ch in morse)
        {
            if (ch == '.') w += dotD + gapElemento;
            else if (ch == '-') w += dashW + gapElemento;
            else if (ch == ' ') w += gapLetra - gapElemento;
        }
        if (w > 0) w -= gapElemento;
        return w;
    }
}
