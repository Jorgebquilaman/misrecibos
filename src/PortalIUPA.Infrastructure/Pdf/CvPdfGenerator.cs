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
        // Generar secciones fijas y contar páginas para calcular números de página de los anexos
        var hoja = GenerarHojaPersonal(datos);
        var antecedentes = GenerarAntecedentesAcademicos(datos);
        var formacion = GenerarFormacion(datos);

        // Secciones de ítems (solo se incluyen si tienen contenido)
        var seccionesItems = new List<byte[]>();
        foreach (var nombreSeccion in new[] { "Antecedentes profesionales y/o artísticos", "Producción", "Otros antecedentes" })
        {
            if (datos.CvItems.Any(i => string.Equals(i.Seccion, nombreSeccion, StringComparison.OrdinalIgnoreCase)))
                seccionesItems.Add(GenerarSeccionItems(datos, nombreSeccion, nombreSeccion));
        }

        var pagHoja = ContarPaginas(hoja);
        var pagAnt = ContarPaginas(antecedentes);
        var pagForm = ContarPaginas(formacion);
        var pagSecciones = seccionesItems.Sum(ContarPaginas);

        // Páginas de cada adjunto
        var pagAdjs = new List<int>();
        foreach (var a in archivos)
        {
            if (EsImagen(a.Nombre)) pagAdjs.Add(1);
            else
            {
                try { using var ms = new MemoryStream(a.Contenido); pagAdjs.Add(PdfReader.Open(ms, PdfDocumentOpenMode.Import).PageCount); }
                catch { pagAdjs.Add(1); }
            }
        }

        // Generar índice borrador para obtener su paginación (determinista: no depende de los textos de Pág.)
        var indiceBorrador = GenerarIndiceAdjuntos(datos, archivos, null);
        var pagIndice = indiceBorrador.Paginas;

        // Calcular página de inicio de cada adjunto (1-indexed para el lector)
        var paginasDestino = new List<int>();
        int acumulado = 0;
        for (int i = 0; i < archivos.Count; i++)
        {
            int inicio = 1 + pagHoja + pagAnt + pagForm + pagSecciones + pagIndice + acumulado;
            paginasDestino.Add(inicio);
            acumulado += pagAdjs[i];
        }

        // Regenerar índice final con números de página y rects de link (misma paginación que el borrador)
        var indice = archivos.Count > 0 ? GenerarIndiceAdjuntos(datos, archivos, paginasDestino) : indiceBorrador;

        var partes = new List<byte[]> { hoja, antecedentes, formacion };
        partes.AddRange(seccionesItems);
        partes.Add(indice.Pdf);
        foreach (var archivo in archivos)
            partes.Add(EsImagen(archivo.ContentType) ? ImagenAPdf(archivo) : archivo.Contenido);

        var combinado = Combinar(partes);

        // Agregar links internos desde la tabla del índice hacia cada anexo
        if (indice.Links.Count > 0)
        {
            try
            {
                // El índice arranca después de hoja + antecedentes + formación + secciones de ítems
                combinado = AgregarLinksIndice(combinado, pagHoja + pagAnt + pagForm + pagSecciones, indice.Links);
            }
            catch { /* no bloquear la generación si falla el linkado */ }
        }

        return Task.FromResult(combinado);
    }

    private static int ContarPaginas(byte[] pdfBytes)
    {
        try { using var ms = new MemoryStream(pdfBytes); return PdfReader.Open(ms, PdfDocumentOpenMode.Import).PageCount; }
        catch { return 1; }
    }

    /// <summary>Detecta anexos de imagen por ContentType (los nombres del índice ya no llevan extensión).</summary>
    private static bool EsImagen(string contentType) =>
        contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

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

    /// <summary>Agrega anotaciones /Link (GoTo interno) sobre las celdas del índice.
    /// Se abre en modo Modify para no romper las referencias de destino (un re-import las invalida).
    /// AddDocumentLink toma la página de destino 1-based; PdfRectangle usa origen bottom-left (flip de Y).</summary>
    private static byte[] AgregarLinksIndice(byte[] pdfBytes, int paginaIndiceInicio, IReadOnlyList<LinkIndice> links)
    {
        var msIn = new MemoryStream(pdfBytes);
        var doc = PdfReader.Open(msIn, PdfDocumentOpenMode.Modify);
        foreach (var link in links)
        {
            int srcIdx = paginaIndiceInicio + link.PaginaLocal;
            int destPag = link.PaginaDestino; // AddDocumentLink es 1-based
            if (srcIdx < 0 || srcIdx >= doc.PageCount || destPag < 1 || destPag > doc.PageCount) continue;
            var page = doc.Pages[srcIdx];
            double h = page.Height.Point;
            var r = link.Rect;
            page.AddDocumentLink(new PdfRectangle(new XRect(r.X, h - r.Y - r.Height, r.Width, r.Height)), destPag);
        }
        using var msOut = new MemoryStream();
        doc.Save(msOut, false);
        return msOut.ToArray();
    }

    /// <summary>Certificado imagen (jpg/png) convertido a una página A4.</summary>
    private static byte[] ImagenAPdf(ArchivoCertificadoCv archivo)
    {
        try
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
                        col.Item().PaddingTop(8).AlignCenter().Image(archivo.Contenido).FitWidth();
                    });
                });
            }).GeneratePdf();
        }
        catch
        {
            return Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(48);
                    page.DefaultTextStyle(TextStyle.Default.FontSize(10));
                    page.Content().Column(col =>
                    {
                        col.Item().Text(archivo.Nombre).FontSize(9).FontColor(Colors.Grey.Darken2);
                        col.Item().PaddingTop(16).Text("No se pudo renderizar la imagen del certificado.").FontColor(Colors.Red.Medium);
                        col.Item().PaddingTop(4).Text($"Archivo: {archivo.Nombre} ({archivo.ContentType})").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });
            }).GeneratePdf();
        }
    }

    /// <summary>Link interno del índice: página local del índice (0-based), rectángulo en coordenadas
    /// top-left de esa página, y página de destino en el documento combinado (1-based).</summary>
    private sealed record LinkIndice(int PaginaLocal, XRect Rect, int PaginaDestino);

    private sealed record IndiceGenerado(byte[] Pdf, int Paginas, IReadOnlyList<LinkIndice> Links);

    private sealed record FilaIndice(string Num, List<string> Cert, List<string> Inst, List<string> Tipo,
        string Fecha, string Pag, double Alto);

    /// <summary>Página(s) índice con la tabla de documentación adjunta (certificados, antecedentes,
    /// experiencias e ítems). Se dibuja con PdfSharpCore (no QuestPDF) para conocer las coordenadas
    /// exactas de cada celda y registrar links internos clicables en las columnas Nº y Pág.
    /// hacia la página del anexo dentro del mismo PDF. Se lista un fila por cada archivo anexo.</summary>
    private static IndiceGenerado GenerarIndiceAdjuntos(DatosCvPdf datos, IReadOnlyList<ArchivoCertificadoCv> archivos,
        IReadOnlyList<int>? paginasDestino = null)
    {
        var conPag = paginasDestino != null && paginasDestino.Count >= archivos.Count;

        const double margen = 48;
        const double anchoPag = 595.28;  // A4 en puntos
        const double altoPag = 841.89;
        const double anchoUtil = anchoPag - 2 * margen;

        // Columnas (mismo criterio visual que antes: Nº fija, relativas 3/2.5/1.5/1.5, Pág. fija)
        const double colNum = 24, colPag = 32;
        double unidad = (anchoUtil - colNum - (conPag ? colPag : 0)) / 8.5;
        double colCert = unidad * 3, colInst = unidad * 2.5, colTipo = unidad * 1.5, colFecha = unidad * 1.5;

        var fTitulo = new XFont("Helvetica", 16, XFontStyle.Bold);
        var fSub = new XFont("Helvetica", 9.5);
        var fIntro = new XFont("Helvetica", 9, XFontStyle.Bold);
        var fHeader = new XFont("Helvetica", 8, XFontStyle.Bold);
        var fCelda = new XFont("Helvetica", 8.5);
        var fLink = new XFont("Helvetica", 8.5, XFontStyle.Underline);
        var fFooter = new XFont("Helvetica", 8);

        var brushTexto = XBrushes.Black;
        var brushSub = new XSolidBrush(XColor.FromArgb(255, 0x61, 0x61, 0x61));
        var brushHeaderTx = new XSolidBrush(XColor.FromArgb(255, 0xE8, 0xD9, 0xC5));
        var brushHeaderBg = new XSolidBrush(XColor.FromArgb(255, 0x0F, 0x0A, 0x0B));
        var brushLink = new XSolidBrush(XColor.FromArgb(255, 0x1E, 0x88, 0xE5));
        var penBorde = new XPen(XColor.FromArgb(255, 0xE0, 0xE0, 0xE0), 0.5);
        var penLinea = new XPen(XColor.FromArgb(255, 0xD3, 0xD3, 0xD3), 1);

        // Contexto de medición (misma tipografía que el dibujo real) para calcular wraps y paginación
        using var docMedida = new PdfDocument();
        var pagMedida = docMedida.AddPage();
        pagMedida.Size = PdfSharpCore.PageSize.A4;
        using var gfxM = XGraphics.FromPdfPage(pagMedida);

        string subtitulo =
            $"Documentación respaldatoria de {datos.Empleado.ApellidoYNombre} (legajo {datos.Empleado.Legajo}). " +
            $"Emitido el {DateTime.Today:dd/MM/yyyy}.";
        var lineasSub = WrapTexto(gfxM, subtitulo, fSub, anchoUtil, 2);

        // Bloque de encabezado (idéntico en todas las páginas del índice)
        const double hTitulo = 20, hIntro = 12, hHeaderTabla = 16;
        double yTitulo = margen;
        double ySub = yTitulo + hTitulo + 4;
        double hSub = lineasSub.Count * 12;
        double yLinea = ySub + hSub + 8;
        double yIntro = yLinea + 8;
        double yHeaderTabla = yIntro + hIntro + 10;
        double yFilasInicio = yHeaderTabla + hHeaderTabla;
        double limiteY = altoPag - 44;

        // Pre-calcular líneas y alto de cada fila (determinista: no depende del texto de la columna Pág.)
        const double lineaH = 10.5, padCelda = 3;
        var filas = new List<FilaIndice>();
        for (int i = 0; i < archivos.Count; i++)
        {
            var a = archivos[i];
            var lCert = WrapTexto(gfxM, a.Nombre, fCelda, colCert - 2 * padCelda, 2);
            var lInst = WrapTexto(gfxM, a.Institucion ?? "", fCelda, colInst - 2 * padCelda, 2);
            var lTipo = WrapTexto(gfxM, a.Tipo ?? "", fCelda, colTipo - 2 * padCelda, 2);
            int maxLineas = Math.Max(lCert.Count, Math.Max(lInst.Count, lTipo.Count));
            filas.Add(new FilaIndice(
                (i + 1).ToString(), lCert, lInst, lTipo,
                a.Fecha ?? "",
                conPag ? paginasDestino![i].ToString() : "—",
                maxLineas * lineaH + 2 * padCelda));
        }

        // Paginar filas (greedy sobre alturas ya calculadas)
        var paginas = new List<List<int>>();
        var paginaActual = new List<int>();
        double yCursor = yFilasInicio;
        for (int i = 0; i < filas.Count; i++)
        {
            if (yCursor + filas[i].Alto > limiteY && paginaActual.Count > 0)
            {
                paginas.Add(paginaActual);
                paginaActual = new List<int>();
                yCursor = yFilasInicio;
            }
            paginaActual.Add(i);
            yCursor += filas[i].Alto;
        }
        if (paginaActual.Count > 0 || paginas.Count == 0) paginas.Add(paginaActual);

        // Dibujar el documento real
        var doc = new PdfDocument();
        var links = new List<LinkIndice>();
        string intro = $"Se adjuntan {archivos.Count} documento(s). Haga clic en el Nº o en la Pág. para ir directamente al documento.";
        var headers = conPag
            ? new[] { ("Nº", colNum), ("Documento", colCert), ("Institución", colInst), ("Tipo", colTipo), ("Fecha", colFecha), ("Pág.", colPag) }
            : new[] { ("Nº", colNum), ("Documento", colCert), ("Institución", colInst), ("Tipo", colTipo), ("Fecha", colFecha) };

        for (int p = 0; p < paginas.Count; p++)
        {
            var pagina = doc.AddPage();
            pagina.Size = PdfSharpCore.PageSize.A4;
            using var gfx = XGraphics.FromPdfPage(pagina);

            // Encabezado
            gfx.DrawString("Documentación adjunta", fTitulo, brushTexto,
                new XRect(margen, yTitulo, anchoUtil, hTitulo), XStringFormats.TopLeft);
            for (int l = 0; l < lineasSub.Count; l++)
                gfx.DrawString(lineasSub[l], fSub, brushSub,
                    new XRect(margen, ySub + l * 12, anchoUtil, 12), XStringFormats.TopLeft);
            gfx.DrawLine(penLinea, margen, yLinea, margen + anchoUtil, yLinea);
            if (p == 0)
                gfx.DrawString(intro, fIntro, brushTexto,
                    new XRect(margen, yIntro, anchoUtil, hIntro), XStringFormats.TopLeft);

            // Header de la tabla
            double x = margen;
            foreach (var (txt, w) in headers)
            {
                gfx.DrawRectangle(brushHeaderBg, x, yHeaderTabla, w, hHeaderTabla);
                gfx.DrawString(txt, fHeader, brushHeaderTx,
                    new XRect(x + padCelda, yHeaderTabla + 3.5, w - padCelda, hHeaderTabla - 3.5), XStringFormats.TopLeft);
                x += w;
            }

            // Filas
            double yFila = yFilasInicio;
            foreach (var fi in paginas[p])
            {
                var fila = filas[fi];
                x = margen;

                // Nº (link al anexo)
                DibujarCelda(gfx, penBorde, x, yFila, colNum, fila.Alto, new List<string> { fila.Num },
                    conPag ? fLink : fCelda, conPag ? brushLink : brushTexto, lineaH, padCelda);
                if (conPag) links.Add(new LinkIndice(p, new XRect(x, yFila, colNum, fila.Alto), paginasDestino![fi]));
                x += colNum;

                DibujarCelda(gfx, penBorde, x, yFila, colCert, fila.Alto, fila.Cert, fCelda, brushTexto, lineaH, padCelda);
                x += colCert;
                DibujarCelda(gfx, penBorde, x, yFila, colInst, fila.Alto, fila.Inst, fCelda, brushTexto, lineaH, padCelda);
                x += colInst;
                DibujarCelda(gfx, penBorde, x, yFila, colTipo, fila.Alto, fila.Tipo, fCelda, brushTexto, lineaH, padCelda);
                x += colTipo;
                DibujarCelda(gfx, penBorde, x, yFila, colFecha, fila.Alto, new List<string> { fila.Fecha }, fCelda, brushTexto, lineaH, padCelda);
                x += colFecha;

                if (conPag)
                {
                    DibujarCelda(gfx, penBorde, x, yFila, colPag, fila.Alto, new List<string> { fila.Pag }, fLink, brushLink, lineaH, padCelda);
                    links.Add(new LinkIndice(p, new XRect(x, yFila, colPag, fila.Alto), paginasDestino![fi]));
                }

                yFila += fila.Alto;
            }

            // Pie alineado a la izquierda para no solapar la numeración global "Página X de Y" (centrada)
            gfx.DrawString("IUPA – Instituto Universitario Patagónico de las Artes • Portal del Empleado",
                fFooter, brushSub, new XRect(margen, altoPag - 32, anchoUtil, 10), XStringFormats.TopLeft);
        }

        int cantPaginas = doc.PageCount;
        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return new IndiceGenerado(ms.ToArray(), cantPaginas, links);
    }

    private static void DibujarCelda(XGraphics gfx, XPen penBorde, double x, double y, double w, double h,
        List<string> lineas, XFont font, XBrush brush, double lineaH, double pad)
    {
        gfx.DrawRectangle(penBorde, x, y, w, h);
        for (int l = 0; l < lineas.Count; l++)
            gfx.DrawString(lineas[l], font, brush,
                new XRect(x + pad, y + pad + l * lineaH, w - 2 * pad, lineaH), XStringFormats.TopLeft);
    }

    /// <summary>Envuelve texto a un ancho máximo con límite de líneas; la última línea cierra con "…" si quedó texto fuera.</summary>
    private static List<string> WrapTexto(XGraphics gfx, string texto, XFont font, double maxAncho, int maxLineas)
    {
        var lineas = new List<string>();
        if (string.IsNullOrWhiteSpace(texto)) { lineas.Add(""); return lineas; }

        var actual = "";
        foreach (var palabra in texto.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var prueba = actual.Length == 0 ? palabra : actual + " " + palabra;
            if (gfx.MeasureString(prueba, font).Width <= maxAncho)
            {
                actual = prueba;
                continue;
            }
            if (actual.Length > 0)
            {
                if (lineas.Count == maxLineas - 1)
                {
                    lineas.Add(TruncarConElipsis(gfx, actual, font, maxAncho));
                    return lineas;
                }
                lineas.Add(actual);
                actual = palabra;
            }
            // Palabra sola más ancha que la columna (o línea vacía que no entra)
            if (gfx.MeasureString(actual, font).Width > maxAncho)
            {
                if (lineas.Count == maxLineas - 1)
                {
                    lineas.Add(TruncarConElipsis(gfx, actual, font, maxAncho));
                    return lineas;
                }
                lineas.Add(TruncarPalabra(gfx, actual, font, maxAncho));
                actual = "";
            }
        }
        if (actual.Length > 0 && lineas.Count < maxLineas)
            lineas.Add(actual);
        if (lineas.Count == 0) lineas.Add("");
        return lineas;
    }

    private static string TruncarPalabra(XGraphics gfx, string palabra, XFont font, double maxAncho)
    {
        while (palabra.Length > 1 && gfx.MeasureString(palabra, font).Width > maxAncho)
            palabra = palabra[..^1];
        return palabra;
    }

    private static string TruncarConElipsis(XGraphics gfx, string texto, XFont font, double maxAncho)
    {
        var t = texto + "…";
        while (t.Length > 1 && gfx.MeasureString(t, font).Width > maxAncho)
            t = t[..^2] + "…";
        return t;
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

    /// <summary>Genera una página de CV con los ítems de una sección, agrupados por categoría.</summary>
    private static byte[] GenerarSeccionItems(DatosCvPdf datos, string titulo, string seccionNombre)
    {
        var items = datos.CvItems.Where(i => string.Equals(i.Seccion, seccionNombre, StringComparison.OrdinalIgnoreCase))
            .OrderBy(i => i.Categoria).ThenByDescending(i => i.FechaDesde)
            .ToList();

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
                    col.Item().PaddingTop(8).Text(titulo).FontSize(13).Bold();
                    col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().Column(col =>
                {
                    col.Item().Text($"Actualizado al {DateTime.Today:dd/MM/yyyy}")
                        .FontSize(9).FontColor(Colors.Grey.Darken2);

                    if (items.Count == 0)
                    {
                        col.Item().PaddingTop(8).Text($"Sin registros en {titulo.ToLowerInvariant()}.")
                            .FontColor(Colors.Grey.Darken1);
                    }
                    else
                    {
                        foreach (var grupo in items.GroupBy(i => i.Categoria).OrderBy(g => g.Key, StringComparer.CurrentCulture))
                        {
                            col.Item().PaddingTop(12).Text(grupo.Key).FontSize(11).Bold();
                            foreach (var it in grupo)
                            {
                                var hasta = it.FechaHasta?.ToString("MM/yyyy") ?? "En curso";
                                col.Item().PaddingTop(8).Text(t =>
                                {
                                    t.Span(it.Titulo).SemiBold();
                                    if (!string.IsNullOrWhiteSpace(it.Institucion))
                                        t.Span($" — {it.Institucion}");
                                    t.Span($"  ({it.FechaDesde:MM/yyyy} – {hasta})")
                                        .FontSize(9.5f).FontColor(Colors.Grey.Darken2);
                                });
                                if (!string.IsNullOrWhiteSpace(it.Descripcion))
                                    col.Item().PaddingTop(2).Column(c => RenderMarkdown(c, it.Descripcion));
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
}
