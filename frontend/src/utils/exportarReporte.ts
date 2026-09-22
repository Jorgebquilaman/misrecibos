import JsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import type { RowInput } from 'jspdf-autotable';
import * as XLSX from 'xlsx';
import api from '../api/axios';
import { reportesApi } from '../api';
import type { ReporteCompletoDto, ResultadoReporteDto } from '../types';

function celdaTexto(valor: unknown): string {
  if (valor === null || valor === undefined) return '';
  if (valor instanceof Date) return valor.toLocaleString('es-AR');
  return String(valor);
}

function encabezados(reporte: ReporteCompletoDto, resultado: ResultadoReporteDto): string[] {
  const def = reporte.definicion;
  return def.filas.length > 0 && def.valores.length > 0
    ? [...def.filas.map((f) => f.alias), ...def.valores.map((v) => v.alias)]
    : resultado.columnas;
}

/** Trae el detalle de cada grupo (igual que el visor), hasta 40 grupos. */
async function traerDetalles(
  reporte: ReporteCompletoDto, resultado: ResultadoReporteDto
): Promise<(ResultadoReporteDto | null)[] | null> {
  const def = reporte.definicion;
  const agrupa = def.filas.length > 0 && def.valores.length > 0;
  if (!agrupa || resultado.filas.length === 0) return null;
  const grupos = resultado.filas.slice(0, 40);
  return Promise.all(grupos.map((fila) =>
    reportesApi.detalle(
      reporte.id,
      Object.fromEntries(def.filas.map((n) => [n.alias, fila[n.alias] ?? '']))
    ).catch(() => null)
  ));
}

const LIMITE_GRUPOS = 40;
const FILAS_POR_GRUPO = 50;

/** Agrupa las filas de detalle por los niveles definidos (modo detalle, sin agregaciones). */
function seccionesDetalle(
  reporte: ReporteCompletoDto, resultado: ResultadoReporteDto
): { clave: string; filas: Record<string, unknown>[] }[] {
  const def = reporte.definicion;
  const grupos = new Map<string, Record<string, unknown>[]>();
  for (const f of resultado.filas.slice(0, 3000)) {
    const clave = def.filas.map((n) => celdaTexto(f[n.alias] ?? '—')).join(' / ');
    if (!grupos.has(clave)) grupos.set(clave, []);
    const arr = grupos.get(clave)!;
    if (arr.length < FILAS_POR_GRUPO) arr.push(f);
  }
  return Array.from(grupos.entries()).slice(0, LIMITE_GRUPOS).map(([clave, filas]) => ({ clave, filas }));
}

function columnasDetalle(
  reporte: ReporteCompletoDto, resultado: ResultadoReporteDto
): string[] {
  return resultado.columnas.filter((c) => !reporte.definicion.filas.some((n) => n.alias === c));
}

export async function exportarReporteAExcel(
  reporte: ReporteCompletoDto, resultado: ResultadoReporteDto
): Promise<void> {
  const def = reporte.definicion;
  const enc = encabezados(reporte, resultado);
  const hojaDatos: unknown[][] = [];

  const agrupa = def.filas.length > 0 && def.valores.length > 0;
  if (def.filas.length > 0 && def.valores.length === 0) {
    // Modo detalle con agrupamiento: secciones por grupo
    const colsDetalle = columnasDetalle(reporte, resultado);
    hojaDatos.push(colsDetalle);
    for (const seccion of seccionesDetalle(reporte, resultado)) {
      hojaDatos.push([`► ${seccion.clave} (${seccion.filas.length})`]);
      seccion.filas.forEach((f) => hojaDatos.push(colsDetalle.map((c) => f[c] ?? '')));
    }
  } else if (!agrupa) {
    hojaDatos.push(enc, ...resultado.filas.map((f) => enc.map((c) => f[c] ?? '')));
  } else {
    hojaDatos.push(enc);
    const detalles = await traerDetalles(reporte, resultado);
    resultado.filas.slice(0, LIMITE_GRUPOS).forEach((fila, i) => {
      hojaDatos.push(enc.map((c) => fila[c] ?? ''));
      const det = detalles?.[i];
      if (det && det.filas.length > 0) {
        hojaDatos.push(det.columnas.map((c) => `${c} (detalle)`));
        det.filas.forEach((f) => hojaDatos.push(det.columnas.map((c) => f[c] ?? '')));
      }
    });
    if (resultado.filas.length > LIMITE_GRUPOS)
      hojaDatos.push([`(detalle omitido para los demás ${resultado.filas.length - LIMITE_GRUPOS} grupos)`]);
  }

  if (def.valores.length > 0 && resultado.totales) {
    hojaDatos.push([
      'Total general',
      ...enc.slice(1).map((c) => resultado.totales?.[c] ?? '')
    ]);
  }

  const hoja = XLSX.utils.aoa_to_sheet(hojaDatos);
  const libro = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(libro, hoja, reporte.nombre.slice(0, 31) || 'reporte');
  XLSX.writeFile(libro, `${reporte.nombre || 'reporte'}.xlsx`);
}

export async function exportarReporteAPdf(
  reporte: ReporteCompletoDto, resultado: ResultadoReporteDto
): Promise<void> {
  const def = reporte.definicion;
  const enc = encabezados(reporte, resultado);
  const doc = new JsPDF({ orientation: enc.length > 6 ? 'landscape' : 'portrait' });
  doc.setFontSize(14);
  doc.text(reporte.nombre, 14, 16);
  doc.setFontSize(9);
  doc.text(`Emitido: ${new Date().toLocaleString('es-AR')}`, 14, 22);

  const agrupa = def.filas.length > 0 && def.valores.length > 0;
  if (def.filas.length > 0 && def.valores.length === 0) {
    const colsDetalle = columnasDetalle(reporte, resultado);
    const cuerpo: RowInput[] = [];
    for (const seccion of seccionesDetalle(reporte, resultado)) {
      cuerpo.push({
        content: `${seccion.clave} (${seccion.filas.length})`,
        colSpan: Math.max(1, colsDetalle.length),
        styles: { fontStyle: 'bold', fillColor: [230, 240, 255] }
      } as RowInput);
      seccion.filas.forEach((f) => cuerpo.push(colsDetalle.map((c) => celdaTexto(f[c]))));
    }
    autoTable(doc, {
      head: [colsDetalle],
      body: cuerpo,
      startY: 28,
      styles: { fontSize: 8, cellPadding: 2 },
      headStyles: { fillColor: [37, 99, 235] },
      theme: 'grid'
    });
  } else if (!agrupa) {
    const cuerpo = resultado.filas.map((f) => enc.map((c) => celdaTexto(f[c] ?? '')));
    const pies = def.valores.length > 0 && resultado.totales
      ? [['Total general', ...enc.slice(1).map((c) => celdaTexto(resultado.totales?.[c] ?? ''))]]
      : undefined;
    autoTable(doc, {
      head: [enc],
      body: cuerpo,
      foot: pies,
      startY: 28,
      styles: { fontSize: 8, cellPadding: 2 },
      headStyles: { fillColor: [37, 99, 235] },
      footStyles: { fontStyle: 'bold' }
    });
  } else {
    const detalles = await traerDetalles(reporte, resultado);
    const cuerpo: RowInput[] = [];
    resultado.filas.slice(0, LIMITE_GRUPOS).forEach((fila, i) => {
      cuerpo.push({
        content: enc.map((c) => `${c}: ${celdaTexto(fila[c] ?? '')}`).join('   '),
        colSpan: Math.max(1, def.filas.length + def.valores.length),
        styles: { fontStyle: 'bold', fillColor: [230, 240, 255] }
      } as RowInput);
      const det = detalles?.[i];
      if (det && det.filas.length > 0) {
        cuerpo.push(det.columnas.map(String));
        det.filas.forEach((f) => cuerpo.push(det.columnas.map((c) => celdaTexto(f[c]))));
      }
    });
    if (resultado.filas.length > LIMITE_GRUPOS)
      cuerpo.push({
        content: `(detalle omitido para los demás ${resultado.filas.length - LIMITE_GRUPOS} grupos)`,
        colSpan: Math.max(1, def.filas.length + def.valores.length)
      } as RowInput);
    autoTable(doc, {
      body: cuerpo,
      startY: 28,
      styles: { fontSize: 8, cellPadding: 2 },
      theme: 'grid'
    });
    if (def.valores.length > 0 && resultado.totales) {
      autoTable(doc, {
        body: [['Total general', ...enc.slice(1).map((c) => celdaTexto(resultado.totales?.[c] ?? ''))]],
        startY: (doc as unknown as { lastAutoTable: { finalY: number } }).lastAutoTable.finalY + 4,
        styles: { fontSize: 9, fontStyle: 'bold' },
        theme: 'plain'
      });
    }
  }
  const nombreArchivo = `${reporte.nombre || 'reporte'}.pdf`;
  try {
    // Generamos el PDF en el cliente y lo mandamos a marcar con trazabilidad (Morse, footer-right).
    const pdfBytes = doc.output('arraybuffer');
    const form = new FormData();
    form.append('archivo', new Blob([pdfBytes], { type: 'application/pdf' }), nombreArchivo);
    const respuesta = await api.post(`/reportes-builder/exportar/marcar`, form, {
      responseType: 'blob'
    });
    const url = URL.createObjectURL(respuesta.data as Blob);
    const enlace = document.createElement('a');
    enlace.href = url;
    enlace.download = nombreArchivo;
    enlace.click();
    URL.revokeObjectURL(url);
  } catch (e) {
    console.error('Fallo al marcar con trazabilidad, descargamos sin marca', e);
    doc.save(nombreArchivo);
  }
}
