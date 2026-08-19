using ClosedXML.Excel;
using PortalIUPA.Application.Common;
using PortalIUPA.Application.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PortalIUPA.Infrastructure.External.Exports;

/// <summary>Exporta un reporte tabular genérico a XLSX (ClosedXML) o PDF (QuestPDF).</summary>
public sealed class ExportadorTabular : IExportadorTabular
{
    public ExportadorTabular()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<ArchivoExportado> GenerarAsync(ReporteTabularDto reporte, string formato,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var nombre = $"Reporte_{Sanitizar(reporte.Titulo)}_{DateTime.Today:yyyyMMdd}";

        if (formato.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(new ArchivoExportado(GenerarPdf(reporte), "application/pdf", $"{nombre}.pdf"));

        if (formato.Equals("xlsx", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(new ArchivoExportado(GenerarXlsx(reporte),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{nombre}.xlsx"));

        throw new ArgumentException("Formato no soportado. Usá 'xlsx' o 'pdf'.");
    }

    private static byte[] GenerarXlsx(ReporteTabularDto reporte)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Reporte");

        hoja.Cell(1, 1).Value = reporte.Titulo;
        hoja.Cell(1, 1).Style.Font.Bold = true;
        hoja.Cell(1, 1).Style.Font.FontSize = 14;
        hoja.Range(1, 1, 1, Math.Max(1, reporte.Columnas.Count)).Merge();

        hoja.Cell(2, 1).Value = reporte.Subtitulo;
        hoja.Range(2, 1, 2, Math.Max(1, reporte.Columnas.Count)).Merge();

        var filaInicio = reporte.Resumen is null ? 4 : 5;
        for (var i = 0; i < reporte.Columnas.Count; i++)
            hoja.Cell(filaInicio, i + 1).Value = reporte.Columnas[i];

        var fila = filaInicio + 1;
        foreach (var filaDatos in reporte.Filas)
        {
            for (var i = 0; i < reporte.Columnas.Count; i++)
                hoja.Cell(fila, i + 1).Value = filaDatos.ElementAtOrDefault(i) ?? "";
            fila++;
        }

        if (reporte.Resumen is not null)
        {
            hoja.Cell(fila + 1, 1).Value = reporte.Resumen;
            hoja.Cell(fila + 1, 1).Style.Font.Bold = true;
            hoja.Range(fila + 1, 1, fila + 1, Math.Max(1, reporte.Columnas.Count)).Merge();
        }

        var rango = hoja.Range(filaInicio, 1, Math.Max(filaInicio, fila - 1), Math.Max(1, reporte.Columnas.Count));
        rango.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        rango.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        hoja.Row(filaInicio).Style.Font.Bold = true;
        hoja.Row(filaInicio).Style.Fill.BackgroundColor = XLColor.FromHtml("#0F0A0B");
        hoja.Row(filaInicio).Style.Font.FontColor = XLColor.FromHtml("#E8D9C5");
        hoja.SheetView.FreezeRows(filaInicio);
        hoja.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return ms.ToArray();
    }

    private static byte[] GenerarPdf(ReporteTabularDto reporte)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(18);
                page.DefaultTextStyle(ts => ts.FontSize(8));

                page.Header().Text(reporte.Titulo)
                    .FontSize(14).SemiBold();

                page.Content().Column(col =>
                {
                    col.Item().Text(reporte.Subtitulo).FontSize(9).FontColor(Colors.Grey.Darken1);
                    if (reporte.Resumen is not null)
                        col.Item().PaddingTop(6).Text(reporte.Resumen).FontSize(9).SemiBold();

                    col.Item().PaddingTop(8).Table(tabla =>
                    {
                        tabla.ColumnsDefinition(c =>
                        {
                            foreach (var _ in reporte.Columnas)
                                c.RelativeColumn(1f);
                        });

                        tabla.Header(h =>
                        {
                            foreach (var enc in reporte.Columnas)
                                h.Cell().Background("#0F0A0B").Padding(3).Text(t =>
                                    t.Span(enc).SemiBold().FontColor("#E8D9C5").FontSize(7));
                        });

                        foreach (var fila in reporte.Filas)
                        {
                            foreach (var valor in fila)
                                tabla.Cell().BorderColor(Colors.Grey.Lighten2).Border(0.5f).Padding(2)
                                    .Text(valor).FontSize(7);
                        }
                    });
                });

                page.Footer().AlignRight().Text(x =>
                {
                    x.Span("Página ").FontSize(8);
                    x.CurrentPageNumber();
                    x.Span(" de ");
                    x.TotalPages();
                });
            });
        });

        return doc.GeneratePdf();
    }

    private static string Sanitizar(string texto)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in texto)
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        return sb.ToString();
    }
}