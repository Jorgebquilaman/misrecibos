namespace PortalIUPA.Infrastructure.Pdf.Trazabilidad;

public enum TrazabilidadPosicion
{
    HeaderLeft,
    HeaderCenter,
    HeaderRight,
    FooterLeft,
    FooterCenter,
    FooterRight
}

public enum TrazabilidadCodificacion
{
    Morse,
    Barcode
}

public sealed record TrazabilidadResultado(
    string TraceId,
    string InputPdf,
    string OutputPdf,
    string InputHashSha256,
    string UserId,
    DateTime FechaHora,
    TrazabilidadCodificacion Encoding,
    TrazabilidadPosicion Position);

public sealed record TrazabilidadRegistro(
    string TraceId,
    string UserId,
    string InputFile,
    string InputHashSha256,
    DateTime FechaHora,
    string Encoding,
    string Position,
    string OutputFile,
    string? OutputHashSha256 = null);

public sealed record TrazabilidadDecodificacionResultado(
    string TraceId,
    TrazabilidadCodificacion Encoding,
    string Fuente);

public static class TrazabilidadPosicionParser
{
    public static TrazabilidadPosicion Parse(string s) => s.Trim().ToLowerInvariant() switch
    {
        "header-left" or "cabecera-izquierda" => TrazabilidadPosicion.HeaderLeft,
        "header-center" or "cabecera-centro" => TrazabilidadPosicion.HeaderCenter,
        "header-right" or "cabecera-derecha" => TrazabilidadPosicion.HeaderRight,
        "footer-left" or "pie-izquierdo" => TrazabilidadPosicion.FooterLeft,
        "footer-center" or "pie-centro" => TrazabilidadPosicion.FooterCenter,
        "footer-right" or "pie-derecho" => TrazabilidadPosicion.FooterRight,
        _ => throw new ArgumentException($"Posición no válida: {s}. Use header-left|center|right o footer-left|center|right")
    };

    public static string ToDisplay(TrazabilidadPosicion p) => p switch
    {
        TrazabilidadPosicion.HeaderLeft => "header-left",
        TrazabilidadPosicion.HeaderCenter => "header-center",
        TrazabilidadPosicion.HeaderRight => "header-right",
        TrazabilidadPosicion.FooterLeft => "footer-left",
        TrazabilidadPosicion.FooterCenter => "footer-center",
        TrazabilidadPosicion.FooterRight => "footer-right",
        _ => p.ToString()
    };
}
