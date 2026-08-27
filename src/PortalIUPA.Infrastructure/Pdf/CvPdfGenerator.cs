using System.Text.RegularExpressions;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using PortalIUPA.Domain.Ports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PortalIUPA.Infrastructure.Pdf;

/// <summary>Genera el CV del empleado: resumen con fecha + los certificados adjuntos en un único PDF.</summary>
public sealed partial class CvPdfGenerator : IGeneradorPdfCv
{
    private static readonly Regex FormatoInline = new(
        @"(\*\*(?<b>.+?)\*\*)|(__(?<u>.+?)__)|(\*(?<i>[^*\n]+?)\*)|(~~(?<s>.+?)~~)",
        RegexOptions.Compiled);

    /// <summary>Renderiza texto con formato Markdown básico (**negrita**, *cursiva*, __subrayado__,
    /// ~~tachado~~, listas con "-" y saltos de línea).</summary>
    private static void RenderMarkdown(ColumnDescriptor col, string texto)
    {
            foreach (var lineaCruda in texto.Replace("\r\n", "\n").Split('\n'))
            {
                var linea = lineaCruda.TrimEnd();
                if (linea.Length == 0)
                {
                    col.Item().PaddingVertical(2);
                    continue;
                }

                if (linea.StartsWith("- ") || linea.StartsWith("* "))
                {
                    col.Item().PaddingLeft(14).Row(row =>
                    {
                        row.AutoItem().Text("•  ");
                        row.RelativeItem().Text(tx => RenderInline(tx, linea[2..]));
                    });
                    continue;
                }

                col.Item().Text(tx => RenderInline(tx, linea));
            }
    }

    private static void RenderInline(TextDescriptor tx, string texto)
    {
        var posicion = 0;
        foreach (Match m in FormatoInline.Matches(texto))
        {
            if (m.Index > posicion)
                tx.Span(texto[posicion..m.Index]);

            if (m.Groups["b"].Success) tx.Span(m.Groups["b"].Value).Bold();
            else if (m.Groups["u"].Success) tx.Span(m.Groups["u"].Value).Underline();
            else if (m.Groups["i"].Success) tx.Span(m.Groups["i"].Value).Italic();
            else if (m.Groups["s"].Success) tx.Span(m.Groups["s"].Value).Strikethrough();

            posicion = m.Index + m.Length;
        }

        if (posicion < texto.Length)
            tx.Span(texto[posicion..]);
    }

    static CvPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<byte[]> GenerarAsync(DatosCvPdf datos, IReadOnlyList<ArchivoCertificadoCv> archivos,
        CancellationToken ct = default)
    {
        var partes = new List<byte[]>
        {
            GenerarHojaPersonal(datos),
            GenerarAntecedentesAcademicos(datos),
            GenerarFormacion(datos),
            GenerarIndiceAdjuntos(datos)
        };

        foreach (var archivo in archivos)
            partes.Add(EsImagen(archivo.Nombre) ? ImagenAPdf(archivo) : archivo.Contenido);

        return Task.FromResult(Combinar(partes));
    }

    private static bool EsImagen(string nombre) =>
        nombre.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
        nombre.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
        nombre.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

    private static byte[] Combinar(IEnumerable<byte[]> partes)
    {
        var salida = new PdfDocument();
        foreach (var parte in partes)
        {
            using var stream = new MemoryStream(parte);
            var doc = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
            foreach (var pagina in doc.Pages)
                salida.AddPage(pagina);
        }

        // Numeración global "Página X de Y" al pie de cada hoja
        var total = salida.PageCount;
        for (var i = 0; i < total; i++)
        {
            using var gfx = XGraphics.FromPdfPage(salida.Pages[i]);
            var fuente = new XFont("Helvetica", 8);
            gfx.DrawString($"Página {i + 1} de {total}", fuente, XBrushes.Gray,
                new XRect(0, salida.Pages[i].Height.Point - 22, salida.Pages[i].Width.Point, 14),
                XStringFormats.Center);
        }

        using var ms = new MemoryStream();
        salida.Save(ms, false);
        return ms.ToArray();
    }

    /// <summary>Certificado imagen (jpg/png) convertido a una página A4.</summary>
    private static byte[] ImagenAPdf(ArchivoCertificadoCv archivo)
    {
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(TextStyle.Default.FontSize(10));
                page.Content().Column(col =>
                {
                    col.Item().Text(archivo.Nombre).FontSize(9).FontColor(Colors.Grey.Darken2);
                    col.Item().PaddingTop(8).AlignMiddle().MaxHeight(720).Image(archivo.Contenido);
                });
            });
        }).GeneratePdf();
    }

    /// <summary>Página índice que lista los certificados adjuntados con su fecha completa.</summary>
    private static byte[] GenerarIndiceAdjuntos(DatosCvPdf datos)
    {
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(48);
                page.DefaultTextStyle(TextStyle.Default.FontSize(10.5f));

                page.Header().Column(col =>
                {
                    col.Item().Text("Certificados adjuntos").FontSize(16).Bold();
                    col.Item().PaddingTop(4).Text(
                            $"Documentación respaldatoria de {datos.Empleado.ApellidoYNombre} (legajo {datos.Empleado.Legajo}). " +
                            $"Emitido el {DateTime.Today:dd/MM/yyyy}.")
                        .FontSize(9.5f).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Item().Text($"Se adjuntan {datos.Certificados.Count} certificado(s), en el orden de la tabla anterior:")
                        .SemiBold();

                    col.Item().PaddingTop(10).Table(tabla =>
                    {
                        tabla.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(22);
                            c.RelativeColumn(3);
                            c.RelativeColumn(2.5f);
                            c.RelativeColumn(1.5f);
                            c.RelativeColumn(1.5f);
                        });

                        tabla.Header(h =>
                        {
                            foreach (var enc in new[] { "Nº", "Certificado", "Institución", "Tipo", "Fecha obtención" })
                                h.Cell().Background("#0F0A0B").Padding(4).Text(t =>
                                    t.Span(enc).SemiBold().FontColor("#E8D9C5").FontSize(8));
                        });

                        var i = 0;
                        foreach (var cert in datos.Certificados.OrderByDescending(c => c.FechaObtencion))
                        {
                            i++;
                            void Celda(string texto)
                            {
                                tabla.Cell().BorderColor(Colors.Grey.Lighten2).Border(0.5f).Padding(3)
                                    .Text(texto).FontSize(8.5f);
                            }
                            Celda(i.ToString());
                            Celda(cert.Nombre);
                            Celda(cert.Institucion);
                            Celda(cert.Tipo);
                            Celda(cert.FechaObtencion.ToString("dd/MM/yyyy"));
                        }
                    });
                });

                page.Footer().AlignCenter()
                    .Text("IUPA – Instituto Universitario Patagónico de las Artes • Portal del Empleado")
                    .FontSize(8).FontColor(Colors.Grey.Darken2);
            });
        }).GeneratePdf();
    }

    /// <summary>Primera hoja: datos personales, resumen profesional y experiencia laboral.</summary>
    private static byte[] GenerarHojaPersonal(DatosCvPdf datos)
    {
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(48);
                page.DefaultTextStyle(TextStyle.Default.FontSize(10.5f));

                page.Header().Column(col =>
                {
                    col.Item().Text(datos.Empleado.ApellidoYNombre).FontSize(20).Bold();

                    void Dato(string etiqueta, string valor)
                    {
                        col.Item().PaddingTop(2).Text(t =>
                        {
                            t.Span($"{etiqueta}: ").FontSize(10).FontColor(Colors.Grey.Darken2);
                            t.Span(valor).FontSize(10).Bold();
                        });
                    }

                    Dato("Legajo", datos.Empleado.Legajo.ToString());
                    if (!string.IsNullOrWhiteSpace(datos.Empleado.Documento))
                        Dato("DNI", datos.Empleado.Documento);
                    if (!string.IsNullOrWhiteSpace(datos.Empleado.Area))
                        Dato("Área", datos.Empleado.Area);
                    if (!string.IsNullOrWhiteSpace(datos.Empleado.Telefono))
                        Dato("Teléfono", datos.Empleado.Telefono);
                    Dato("Email", datos.Empleado.Correo);

                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingTop(16).Column(col =>
                {
                    var hayResumen = !string.IsNullOrWhiteSpace(datos.Empleado.Observaciones);

                    if (hayResumen)
                    {
                        col.Item().Text("Resumen profesional").FontSize(13).Bold();
                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                        col.Item().PaddingTop(6).Column(c => RenderMarkdown(c, datos.Empleado.Observaciones));
                    }

                    if (datos.Experiencias.Count > 0)
                    {
                        col.Item().PaddingTop(hayResumen ? 16 : 0).Text("Experiencia profesional").FontSize(13).Bold();
                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

                        foreach (var exp in datos.Experiencias)
                        {
                            var hasta = exp.FechaHasta?.ToString("MM/yyyy") ?? "Actualidad";
                            col.Item().PaddingTop(8).Text(t =>
                            {
                                t.Span(exp.Puesto).Bold();
                                t.Span($" — {exp.Institucion}");
                                t.Span($"  ({exp.FechaDesde:MM/yyyy} – {hasta})")
                                    .FontSize(9.5f).FontColor(Colors.Grey.Darken2);
                            });

                            if (!string.IsNullOrWhiteSpace(exp.Descripcion))
                                col.Item().Column(c => RenderMarkdown(c, exp.Descripcion));
                        }
                    }
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("IUPA – Instituto Universitario Patagónico de las Artes • Portal del Empleado")
                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                    row.AutoItem().Text($"Emitido el {DateTime.Today:dd/MM/yyyy}")
                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                });
            });
        }).GeneratePdf();
    }

    /// <summary>Segunda hoja: formación (certificados agrupados por tipo).</summary>
    private static byte[] GenerarFormacion(DatosCvPdf datos)
    {
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(48);
                page.DefaultTextStyle(TextStyle.Default.FontSize(10.5f));

                page.Header().Column(col =>
                {
                    col.Item().Text(datos.Empleado.ApellidoYNombre).FontSize(14).Bold();
                    col.Item().PaddingTop(8).Text("Formación").FontSize(13).Bold();
                    col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().Column(col =>
                {
                    col.Item().Text($"Actualizado al {DateTime.Today:dd/MM/yyyy}")
                        .FontSize(9).FontColor(Colors.Grey.Darken2);

                    if (datos.Certificados.Count == 0)
                    {
                        col.Item().PaddingTop(8).Text("Sin certificados registrados.")
                            .FontColor(Colors.Grey.Darken1);
                    }
                    else
                    {
                        // Agrupados por tipo; dentro de cada grupo ordenados por fecha (más recientes primero).
                        foreach (var grupo in datos.Certificados
                                     .GroupBy(c => c.Tipo)
                                     .OrderBy(g => OrdenTipo(g.Key)))
                        {
                            col.Item().PaddingTop(12).Text(EtiquetaTipo(grupo.Key)).FontSize(11).Bold();
                            col.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);

                            foreach (var cert in grupo.OrderByDescending(c => c.FechaObtencion))
                            {
                                col.Item().PaddingTop(6).Column(c1 =>
                                {
                                    c1.Item().Text($"{cert.Nombre} ({cert.FechaObtencion:dd/MM/yyyy})").SemiBold();
                                    c1.Item().Text(cert.Institucion).FontSize(9.5f)
                                        .FontColor(Colors.Grey.Darken1);
                                });
                            }
                        }
                    }
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("IUPA – Instituto Universitario Patagónico de las Artes • Portal del Empleado")
                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                    row.AutoItem().Text($"Emitido el {DateTime.Today:dd/MM/yyyy}")
                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                });
            });
        }).GeneratePdf();
    }

    private static string EtiquetaTipo(string tipo) => tipo switch
    {
        "Curso" => "Curso",
        "Taller" => "Taller",
        "Diplomatura" => "Diplomatura",
        "Carrera" => "Carrera de grado",
        "Posgrado" => "Posgrado",
        _ => "Otro"
    };

    private static int OrdenTipo(string tipo) => tipo switch
    {
        "Posgrado" => 0,
        "Carrera" => 1,
        "Diplomatura" => 2,
        "Curso" => 3,
        "Taller" => 4,
        _ => 5
    };

    /// <summary>Hoja de antecedentes académicos: títulos con fecha, institución, nivel y descripción.</summary>
    private static byte[] GenerarAntecedentesAcademicos(DatosCvPdf datos)
    {
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(48);
                page.DefaultTextStyle(TextStyle.Default.FontSize(10.5f));

                page.Header().Column(col =>
                {
                    col.Item().Text(datos.Empleado.ApellidoYNombre).FontSize(14).Bold();
                    col.Item().PaddingTop(8).Text("Antecedentes académicos").FontSize(13).Bold();
                    col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().Column(col =>
                {
                    col.Item().Text($"Actualizado al {DateTime.Today:dd/MM/yyyy}")
                        .FontSize(9).FontColor(Colors.Grey.Darken2);

                    if (datos.Antecedentes.Count == 0)
                    {
                        col.Item().PaddingTop(8).Text("Sin antecedentes académicos registrados.")
                            .FontColor(Colors.Grey.Darken1);
                    }
                    else
                    {
                        foreach (var ant in datos.Antecedentes)
                        {
                            var hasta = ant.FechaHasta?.ToString("MM/yyyy") ?? "En curso";
                            col.Item().PaddingTop(10).Text(t =>
                            {
                                t.Span(ant.Titulo).SemiBold();
                                t.Span($" — {ant.Institucion}");
                                t.Span($"  ({ant.FechaDesde:MM/yyyy} – {hasta})")
                                    .FontSize(9.5f).FontColor(Colors.Grey.Darken2);
                            });
                            col.Item().Text(EtiquetaNivel(ant.Nivel)).FontSize(9).FontColor(Colors.Grey.Darken1).Italic();
                            if (!string.IsNullOrWhiteSpace(ant.Descripcion))
                                col.Item().PaddingTop(2).Column(c => RenderMarkdown(c, ant.Descripcion));
                        }
                    }
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("IUPA – Instituto Universitario Patagónico de las Artes • Portal del Empleado")
                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                    row.AutoItem().Text($"Emitido el {DateTime.Today:dd/MM/yyyy}")
                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                });
            });
        }).GeneratePdf();
    }

    private static string EtiquetaNivel(string nivel) => nivel switch
    {
        "Secundario" => "Secundario",
        "Terciario" => "Terciario",
        "Universitario" => "Universitario",
        "Posgrado" => "Posgrado",
        "Maestria" => "Maestría",
        "Doctorado" => "Doctorado",
        _ => "Otro"
    };
}
