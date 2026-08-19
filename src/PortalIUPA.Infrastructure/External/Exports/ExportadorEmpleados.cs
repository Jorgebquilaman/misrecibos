using ClosedXML.Excel;
using PortalIUPA.Application.Common;
using PortalIUPA.Application.DTOs;
using PortalIUPA.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PortalIUPA.Infrastructure.External.Exports;

/// <summary>Exporta la lista de empleados a XLSX (ClosedXML) o PDF (QuestPDF).</summary>
public sealed class ExportadorEmpleados : IExportadorEmpleados
{
    private static readonly string[] Encabezados = ["Legajo", "Apellido", "Nombre", "DNI", "CUIL", "Correo", "Área", "Roles", "Estado"];

    public ExportadorEmpleados()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<ArchivoExportado> GenerarAsync(IReadOnlyList<EmpleadoDto> empleados, string formato,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var nombre = $"Empleados_{DateTime.Today:yyyyMMdd}";

        if (formato.Equals("pdf", StringComparison.OrdinalIgnoreCase))
        {
            var pdf = GenerarPdf(empleados);
            return Task.FromResult(new ArchivoExportado(pdf, "application/pdf", $"{nombre}.pdf"));
        }

        if (formato.Equals("xlsx", StringComparison.OrdinalIgnoreCase))
        {
            var xlsx = GenerarXlsx(empleados);
            return Task.FromResult(new ArchivoExportado(xlsx,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{nombre}.xlsx"));
        }

        throw new ArgumentException("Formato no soportado. Usá 'xlsx' o 'pdf'.");
    }

    private static byte[] GenerarXlsx(IReadOnlyList<EmpleadoDto> empleados)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Empleados");

        for (var i = 0; i < Encabezados.Length; i++)
            hoja.Cell(1, i + 1).Value = Encabezados[i];

        var fila = 2;
        foreach (var e in empleados)
        {
            hoja.Cell(fila, 1).Value = e.Legajo;
            hoja.Cell(fila, 2).Value = e.Apellido;
            hoja.Cell(fila, 3).Value = e.Nombre;
            hoja.Cell(fila, 4).Value = e.Dni ?? "";
            hoja.Cell(fila, 5).Value = e.Cuil ?? "";
            hoja.Cell(fila, 6).Value = e.Correo;
            hoja.Cell(fila, 7).Value = e.AreaNombre ?? "";
            hoja.Cell(fila, 8).Value = string.Join(", ", e.Roles);
            hoja.Cell(fila, 9).Value = e.Activo ? "Activo" : "Inactivo";
            fila++;
        }

        var rango = hoja.Range(1, 1, Math.Max(1, fila - 1), Encabezados.Length);
        rango.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        rango.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        hoja.Row(1).Style.Font.Bold = true;
        hoja.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#0F0A0B");
        hoja.Row(1).Style.Font.FontColor = XLColor.FromHtml("#E8D9C5");
        hoja.SheetView.FreezeRows(1);
        hoja.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return ms.ToArray();
    }

    private static byte[] GenerarPdf(IReadOnlyList<EmpleadoDto> empleados)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(ts => ts.FontSize(8));

                page.Header().Text("Listado de empleados")
                    .FontSize(14).SemiBold();

                page.Content().Table(tabla =>
                {
                    tabla.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(40);
                        c.RelativeColumn(1.1f);
                        c.RelativeColumn(1.0f);
                        c.RelativeColumn(2.0f);
                        c.RelativeColumn(0.8f);
                        c.RelativeColumn(1.5f);
                        c.ConstantColumn(55);
                    });

                    tabla.Header(h =>
                    {
                        foreach (var enc in new[] { "Legajo", "Apellido", "Nombre", "Correo", "Área", "Roles", "Estado" })
                            h.Cell().Background("#0F0A0B").Padding(3).Text(t =>
                                t.Span(enc).SemiBold().FontColor("#E8D9C5").FontSize(8));
                    });

                    foreach (var e in empleados)
                    {
                        foreach (var valor in new[]
                        {
                            e.Legajo.ToString(), e.Apellido, e.Nombre, e.Correo, e.AreaNombre ?? "-",
                            string.Join(", ", e.Roles.Select(RolEnEspanol)), e.Activo ? "Activo" : "Inactivo"
                        })
                        {
                            tabla.Cell().BorderColor(Colors.Grey.Lighten2).Border(0.5f).Padding(3)
                                .Text(valor).FontSize(8);
                        }
                    }
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

    private static string RolEnEspanol(Rol rol) => rol switch
    {
        Rol.Empleado => "Empleado",
        Rol.Responsable => "Responsable",
        Rol.Rrhh => "RRHH",
        Rol.Administrador => "Administrador",
        Rol.Direccion => "Dirección",
        _ => rol.ToString()
    };
}