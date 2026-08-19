using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.Ports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PortalIUPA.Infrastructure.Pdf;

/// <summary>Genera el PDF del certificado laboral (constancia de trabajo / certificación de servicios).</summary>
public sealed class CertificadoPdfGenerator : IGeneradorPdfCertificado
{
    static CertificadoPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<byte[]> GenerarAsync(DatosCertificadoPdf datos, CancellationToken ct = default)
    {
        var titulo = datos.Tipo == TipoCertificado.ConstanciaTrabajo
            ? "CONSTANCIA DE TRABAJO"
            : "CERTIFICACIÓN DE SERVICIOS";

        var cuerpo = "Se certifica que " + datos.ApellidoYNombre +
                     $", DNI {datos.Dni ?? "—"}, CUIL {datos.Cuil ?? "—"}, legajo Nº {datos.Legajo}, " +
                     $"se desempeña en esta institución en el período comprendido entre el " +
                     $"{datos.Desde:dd/MM/yyyy} y el {datos.Hasta:dd/MM/yyyy}.";

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(48);
                page.DefaultTextStyle(TextStyle.Default.FontSize(11).FontFamily("Helvetica"));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text(datos.Institucion)
                        .FontSize(14).Bold().FontColor(Colors.Blue.Darken3);
                    col.Item().AlignCenter().Text(datos.Sede)
                        .FontSize(9).FontColor(Colors.Grey.Darken2);
                    col.Item().PaddingTop(8).AlignCenter().Text(titulo)
                        .FontSize(16).Bold();
                    col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingTop(24).Column(col =>
                {
                    col.Item().Text(cuerpo).Justify();

                    if (!string.IsNullOrWhiteSpace(datos.Destino))
                        col.Item().PaddingTop(16)
                            .Text($"La presente constancia se emite a los fines de ser presentada ante: {datos.Destino}.")
                            .Justify();

                    col.Item().PaddingTop(24)
                        .Text($"Se expide en General Roca, el {DateTime.Today:dd/MM/yyyy}.")
                        .Justify();

                    col.Item().PaddingTop(64).AlignCenter().Text("Firma y sello de la autoridad competente")
                        .FontSize(10).FontColor(Colors.Grey.Darken2);
                });

                page.Footer().AlignCenter()
                    .Text("IUPA – Instituto Universitario Patagónico de las Artes • Portal del Empleado")
                    .FontSize(8).FontColor(Colors.Grey.Darken2);
            });
        }).GeneratePdf();

        return Task.FromResult(pdf);
    }
}