using PortalIUPA.Domain.Enums;

namespace PortalIUPA.Domain.Ports;

public sealed record DatosCertificadoPdf(
    TipoCertificado Tipo,
    string ApellidoYNombre,
    int Legajo,
    string? Dni,
    string? Cuil,
    DateOnly Desde,
    DateOnly Hasta,
    string? Destino,
    string Institucion = "IUPA – Instituto Universitario Patagónico de las Artes",
    string Sede = "Sede Central – Roca 1683, General Roca, Río Negro");

/// <summary>Genera el PDF del certificado laboral (constancia/certificación) con datos institucionales.</summary>
public interface IGeneradorPdfCertificado
{
    Task<byte[]> GenerarAsync(DatosCertificadoPdf datos, CancellationToken ct = default);
}