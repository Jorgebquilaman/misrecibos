import { useEffect, useMemo, useRef, useState } from 'react';
import JsBarcode from 'jsbarcode';
import {
  AlignCenter, AlignLeft, AlignRight, Bold, Download, FileSpreadsheet, Image as ImageIcon,
  Italic, Loader2, MousePointer2, Save, Type, Underline
} from 'lucide-react';
import { exportarReporteAExcel, exportarReporteAPdf } from '../utils/exportarReporte';
import { reportesApi } from '../api';
import type {
  ElementoDiseno, ReporteCompletoDto, ReporteDiseno, ResultadoReporteDto
} from '../types';

const ANCHO_HOJA = 794;   // A4 @96dpi
const ALTO_HOJA = 1123;

const defPorDefecto = (tipo: ElementoDiseno['tipo'], orden: number): ElementoDiseno => {
  const base: ElementoDiseno = {
    id: `${tipo}-${Date.now()}-${orden}`,
    tipo, x: 40, y: 40 + orden * 50, ancho: 300, alto: 36
  };
  switch (tipo) {
    case 'titulo': return { ...base, texto: 'Título del reporte', tamano: 22, negrita: true, alineacion: 'izq' };
    case 'texto': return { ...base, texto: 'Texto…', tamano: 12, alineacion: 'izq' };
    case 'imagen': return { ...base, ancho: 140, alto: 60, imagen: '' };
    case 'barcode': return { ...base, ancho: 220, alto: 60, valor: '', mostrarTexto: true };
    case 'linea': return { ...base, ancho: 714, alto: 2 };
    case 'tabla': return { ...base, x: 40, y: 180, ancho: 714, alto: 400 };
  }
};

/** Reemplaza placeholders dinámicos en textos: {fecha_hoy} y {nombre_reporte}. */
export function interpolar(texto: string | undefined): string {
  if (!texto) return '';
  return texto
    .replace(/\{fecha_hoy\}/g, new Date().toLocaleDateString('es-AR'))
    .replace(/\{nombre_reporte\}/g, reporteNombreGlobal);
}
let reporteNombreGlobal = '';

/** Mini grilla del resultado dentro del canvas (con detalle por grupo si agrupa). */
export function TablaResultado({ resultado, def, reporteId }: {
  resultado: ResultadoReporteDto;
  def: ReporteCompletoDto['definicion'];
  reporteId?: string;
}) {
  const [orden, setOrden] = useState<{ col: string; dir: 'asc' | 'desc' } | null>(null);
  const toggleOrden = (col: string) =>
    setOrden((prev) => (prev?.col === col ? (prev.dir === 'asc' ? { col, dir: 'desc' } : null) : { col, dir: 'asc' }));
  const filas = useMemo(() => {
    if (!orden) return resultado.filas;
    const { col, dir } = orden;
    return [...resultado.filas].sort((a, b) => {
      const va = a[col], vb = b[col];
      if (va === null || va === undefined) return 1;
      if (vb === null || vb === undefined) return -1;
      if (typeof va === 'number' && typeof vb === 'number') return dir === 'asc' ? va - vb : vb - va;
      const ca = String(va), cb = String(vb);
      return dir === 'asc' ? ca.localeCompare(cb, 'es') : cb.localeCompare(ca, 'es');
    });
  }, [resultado.filas, orden]);
  const esAgrupado = def.filas.length > 0 && def.valores.length > 0;
  const claves = resultado.columnas;
  const totales = resultado.totales ?? {};

  // Detalle por grupo (igual que en la exportación), solo si el reporte agrupa.
  const [detalles, setDetalles] = useState<(ResultadoReporteDto | null)[] | null>(null);
  useEffect(() => {
    if (!esAgrupado || !reporteId || filas.length === 0) { setDetalles(null); return; }
    let cancelado = false;
    Promise.all(filas.slice(0, 40).map((fila) =>
      reportesApi.detalle(
        reporteId,
        Object.fromEntries(def.filas.map((n) => [n.alias, fila[n.alias] ?? '']))
      ).catch(() => null)
    )).then((res) => { if (!cancelado) setDetalles(res); });
    return () => { cancelado = true; };
  }, [esAgrupado, reporteId, filas, def.filas]);

  if (filas.length === 0) return <p className="text-[10px] text-ink-muted">Sin datos para los filtros.</p>;

  const encabezados = esAgrupado
    ? def.filas.map((f) => f.alias).concat(def.valores.map((v) => v.alias))
    : claves;

  return (
    <table className="w-full text-left text-[10px]">
      <thead>
        <tr className="border-b border-black/20">
          {encabezados.map((c) => (
            <th key={c} className="px-1 py-0.5 font-semibold uppercase tracking-wide">
              <button
                onClick={() => toggleOrden(c)}
                className="flex items-center gap-0.5 hover:text-blue-600"
                title="Ordenar por esta columna"
              >
                {c}
                <span className="text-[9px]">
                  {orden?.col === c ? (orden.dir === 'asc' ? '▲' : '▼') : <span className="opacity-30">⇅</span>}
                </span>
              </button>
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {esAgrupado
          ? filas.slice(0, 40).map((f, i) => (
              <FilaGrupoDetalle key={i} fila={f} detalle={detalles?.[i] ?? null} def={def} />
            ))
          : def.filas.length > 0
            ? <SeccionesDetalle filas={filas} niveles={def.filas} clavesDetalle={claves} />
            : filas.map((f, i) => (
              <tr key={i} className="border-b border-black/10">
                {claves.map((c) => (
                  <td key={c} className="px-1 py-0.5">
                    {f[c] instanceof Date ? (f[c] as Date).toLocaleDateString('es-AR') : String(f[c] ?? '')}
                  </td>
                ))}
              </tr>
            ))}
      </tbody>
      {def.valores.length > 0 && (
        <tfoot>
          <tr className="border-t-2 border-black/40 font-semibold">
            <td className="px-1 py-0.5">Total general</td>
            {claves.slice(1).map((c) => (
              <td key={c} className="px-1 py-0.5 tabular-nums">
                {totales[c] !== undefined ? formatearCelda(totales[c]) : ''}
              </td>
            ))}
          </tr>
        </tfoot>
      )}
    </table>
  );
}

function SeccionesDetalle({ filas, niveles, clavesDetalle }: {
  filas: Record<string, unknown>[];
  niveles: { campo: string; alias: string }[];
  clavesDetalle: string[];
}) {
  const grupos = new Map<string, Record<string, unknown>[]>();
  for (const f of filas.slice(0, 3000)) {
    const clave = niveles.map((n) => String(f[n.alias] ?? '—')).join(' / ');
    if (!grupos.has(clave)) grupos.set(clave, []);
    if ((grupos.get(clave) ?? []).length < 50) grupos.get(clave)!.push(f);
  }
  return (
    <>
      {Array.from(grupos.entries()).slice(0, 40).map(([clave, rows]) => (
        <GrupoSeccion key={clave} clave={clave} rows={rows} clavesDetalle={clavesDetalle} />
      ))}
    </>
  );
}

function GrupoSeccion({ clave, rows, clavesDetalle }: {
  clave: string;
  rows: Record<string, unknown>[];
  clavesDetalle: string[];
}) {
  return (
    <>
      <tr className="border-b border-black/10 bg-black/[0.04] font-semibold">
        <td colSpan={clavesDetalle.length} className="px-1 py-0.5">{clave} ({rows.length})</td>
      </tr>
      {rows.map((f, i) => (
        <tr key={i} className="border-b border-black/10">
          {clavesDetalle.map((c) => (
            <td key={c} className="px-1 py-0.5">
              {f[c] instanceof Date ? (f[c] as Date).toLocaleDateString('es-AR') : String(f[c] ?? '')}
            </td>
          ))}
        </tr>
      ))}
    </>
  );
}
function FilaGrupoDetalle({ fila, detalle, def }: {
  fila: Record<string, unknown>;
  detalle: ResultadoReporteDto | null;
  def: ReporteCompletoDto['definicion'];
}) {
  const clavesGrupo = def.filas.map((f) => f.alias).concat(def.valores.map((v) => v.alias));
  return (
    <>
      <tr className="border-b border-black/10 bg-black/[0.04] font-semibold">
        {clavesGrupo.map((c) => (
          <td key={c} className="px-1 py-0.5">
            {fila[c] instanceof Date ? (fila[c] as Date).toLocaleDateString('es-AR') : String(fila[c] ?? '')}
          </td>
        ))}
      </tr>
      {detalle && detalle.filas.length > 0 && (
        <>
          {detalle.filas.slice(0, 50).map((f, i) => (
            <tr key={i} className="border-b border-black/10">
              {detalle.columnas.map((c) => (
                <td key={c} className="px-1 py-0.5">
                  {f[c] instanceof Date ? (f[c] as Date).toLocaleDateString('es-AR') : String(f[c] ?? '')}
                </td>
              ))}
            </tr>
          ))}
          {detalle.filas.length > 50 && (
            <tr><td colSpan={detalle.columnas.length} className="px-1 py-0.5 text-[9px] text-black/40">
              … {detalle.filas.length - 50} filas más</td></tr>
          )}
        </>
      )}
    </>
  );
}

function formatearCelda(v: unknown): string {
  if (v === null || v === undefined) return '—';
  if (typeof v === 'number') return v.toLocaleString('es-AR');
  if (v instanceof Date) return v.toLocaleDateString('es-AR');
  return String(v);
}

function CodigoBarras({ valor, alto, mostrarTexto, ancho }: {
  valor: string; alto: number; mostrarTexto?: boolean; ancho: number;
}) {
  const ref = useRef<SVGSVGElement>(null);
  useEffect(() => {
    if (!ref.current || !valor) return;
    try {
      JsBarcode(ref.current, valor, {
        format: 'CODE128', height: Math.max(30, alto), displayValue: !!mostrarTexto,
        margin: 0, fontSize: 11, width: 1.6
      });
    } catch { /* valor inválido: no renderiza */ }
  }, [valor, alto, mostrarTexto]);
  if (!valor) {
    return <div className="flex h-full items-center justify-center text-[10px] text-ink-muted">código de barras (valor)</div>;
  }
  return <svg ref={ref} style={{ maxWidth: ancho }} />;
}

export function ElementoCanvas({
  el, seleccionado, onMover, onRedimensionar, onSeleccionar, modoEdicion
}: {
  el: ElementoDiseno;
  seleccionado: boolean;
  onSeleccionar: () => void;
  onMover: (x: number, y: number) => void;
  onRedimensionar: (ancho: number, alto: number) => void;
  modoEdicion: boolean;
}) {
  const dragRef = useRef<{ startX: number; startY: number; elX: number; elY: number; modo: 'mover' | 'resize' } | null>(null);

  const iniciar = (e: React.PointerEvent, modo: 'mover' | 'resize') => {
    if (!modoEdicion) return;
    e.stopPropagation();
    e.preventDefault();
    onSeleccionar();
    dragRef.current = { startX: e.clientX, startY: e.clientY, elX: el.x, elY: el.y, modo };
    const mover = (ev: PointerEvent) => {
      if (!dragRef.current) return;
      const dx = ev.clientX - dragRef.current.startX;
      const dy = ev.clientY - dragRef.current.startY;
      if (dragRef.current.modo === 'mover')
        onMover(Math.max(0, dragRef.current.elX + dx), Math.max(0, dragRef.current.elY + dy));
      else
        onRedimensionar(Math.max(12, el.ancho + dx), Math.max(8, el.alto + dy));
    };
    const soltar = () => {
      dragRef.current = null;
      window.removeEventListener('pointermove', mover);
      window.removeEventListener('pointerup', soltar);
    };
    window.addEventListener('pointermove', mover);
    window.addEventListener('pointerup', soltar);
  };

  const estilo: React.CSSProperties = {
    position: 'absolute', left: el.x, top: el.y, width: el.ancho, height: el.alto,
    fontSize: el.tamano ?? 12, fontWeight: el.negrita ? 700 : undefined,
    fontStyle: el.italica ? 'italic' : undefined,
    textDecoration: el.subrayado ? 'underline' : undefined,
    color: el.color ?? undefined,
    textAlign: el.alineacion === 'centro' ? 'center' : el.alineacion === 'der' ? 'right' : 'left',
    cursor: modoEdicion ? 'move' : 'default',
    overflow: 'hidden',
    outline: seleccionado && modoEdicion ? '1px dashed #2563eb' : undefined,
    outlineOffset: 2
  };

  return (
    <div
      onPointerDown={(e) => iniciar(e, 'mover')}
      className="select-none"
      style={estilo}
    >
      {el.tipo === 'titulo' && <h3 className="m-0 h-full w-full leading-tight">{interpolar(el.texto)}</h3>}
      {el.tipo === 'texto' && <p className="m-0 h-full w-full leading-tight">{interpolar(el.texto)}</p>}
      {el.tipo === 'imagen' && (
        el.imagen
          ? <img src={el.imagen} alt="" className="h-full w-full object-contain" draggable={false} />
          : <div className="flex h-full w-full items-center justify-center rounded border border-dashed border-black/30 text-[10px] text-black/40">logo/imagen</div>
      )}
      {el.tipo === 'barcode' && (
        <div className="flex h-full w-full items-center overflow-hidden">
          <CodigoBarras valor={interpolar(el.valor)} alto={el.alto} mostrarTexto={el.mostrarTexto} ancho={el.ancho} />
        </div>
      )}
      {el.tipo === 'linea' && <hr className="m-0 w-full border-0 border-t border-black/50" />}
      {el.tipo === 'tabla' && <div className="h-full w-full" />}
      {modoEdicion && seleccionado && (
        <span
          onPointerDown={(e) => iniciar(e, 'resize')}
          className="absolute -bottom-1 -right-1 h-3 w-3 cursor-se-resize rounded-sm bg-blue-600"
        />
      )}
    </div>
  );
}

export default function ReporteCanvas({ reporte, onCerrar }: {
  reporte: ReporteCompletoDto;
  onCerrar: () => void;
}) {
  const def = reporte.definicion;
  reporteNombreGlobal = reporte.nombre;
  const [diseno, setDiseno] = useState<ReporteDiseno>(() => {
    try {
      const parseado = JSON.parse(reporte.disenoJson) as ReporteDiseno;
      return { elementos: parseado?.elementos ?? [] };
    } catch { return { elementos: [] }; }
  });
  const [resultado, setResultado] = useState<ResultadoReporteDto | null>(null);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [seleccion, setSeleccion] = useState<string | null>(null);
  const [guardando, setGuardando] = useState(false);
  const [vistaPrevia, setVistaPrevia] = useState(false);
  const [exportando, setExportando] = useState(false);

  // Ejecutamos el reporte sin filtros para tener los datos en la hoja.
  useEffect(() => {
    setCargando(true);
    reportesApi.ejecutar(reporte.id, {})
      .then(setResultado)
      .catch((e) => setError(e.response?.data?.error ?? 'Error al ejecutar el reporte'))
      .finally(() => setCargando(false));
  }, [reporte.id]);

  // Aseguramos que siempre exista el elemento tabla (contiene el reporte).
  useEffect(() => {
    if (!diseno.elementos.some((e) => e.tipo === 'tabla')) {
      setDiseno({ elementos: [defPorDefecto('tabla', 0), ...diseno.elementos] });
    }
  }, [diseno.elementos]);

  const seleccionado = diseno.elementos.find((e) => e.id === seleccion) ?? null;
  const idx = useMemo(() => diseno.elementos.length, [diseno.elementos]);

  const actualizar = (id: string, cambios: Partial<ElementoDiseno>) =>
    setDiseno((d) => ({
      elementos: d.elementos.map((e) => (e.id === id ? { ...e, ...cambios } : e))
    }));

  const agregar = (tipo: ElementoDiseno['tipo']) => {
    const nuevo = defPorDefecto(tipo, idx);
    setDiseno((d) => ({ elementos: [...d.elementos, nuevo] }));
    setSeleccion(nuevo.id);
  };

  const agregarImagen = (file: File) => {
    const lector = new FileReader();
    lector.onload = () => {
      const nuevo = { ...defPorDefecto('imagen', idx), imagen: String(lector.result) };
      setDiseno((d) => ({ elementos: [...d.elementos, nuevo] }));
      setSeleccion(nuevo.id);
    };
    lector.readAsDataURL(file);
  };

  const guardar = async () => {
    setGuardando(true);
    setError(null);
    try {
      await reportesApi.guardarDiseno(reporte.id, diseno);
    } catch (e: unknown) {
      const err = e as { response?: { data?: { error?: string } } };
      setError(err.response?.data?.error ?? 'Error al guardar el diseño');
    } finally {
      setGuardando(false);
    }
  };

  const eliminarSeleccion = () => {
    if (!seleccion) return;
    setDiseno((d) => ({ elementos: d.elementos.filter((e) => e.id !== seleccion) }));
    setSeleccion(null);
  };

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <button onClick={onCerrar} className="btn-secondary text-sm">← Volver</button>
        <button onClick={() => setVistaPrevia(!vistaPrevia)} className="btn-secondary flex items-center gap-1 text-sm">
          <MousePointer2 size={14} /> {vistaPrevia ? 'Editar' : 'Vista previa'}
        </button>
        <button onClick={guardar} disabled={guardando} className="btn-primary flex items-center gap-1 text-sm">
          {guardando ? <Loader2 size={14} className="animate-spin" /> : <Save size={14} />} Guardar diseño
        </button>
        {resultado && (
          <>
            <button
              onClick={async () => { setExportando(true); try { await exportarReporteAExcel(reporte, resultado); } finally { setExportando(false); } }}
              disabled={exportando}
              className="btn-secondary flex items-center gap-1 text-sm"
            >
              {exportando ? <Loader2 size={14} className="animate-spin" /> : <FileSpreadsheet size={14} />} XLSX
            </button>
            <button
              onClick={async () => { setExportando(true); try { await exportarReporteAPdf(reporte, resultado); } finally { setExportando(false); } }}
              disabled={exportando}
              className="btn-secondary flex items-center gap-1 text-sm"
            >
              {exportando ? <Loader2 size={14} className="animate-spin" /> : <Download size={14} />} PDF
            </button>
          </>
        )}
        {error && <span className="text-sm text-danger">{error}</span>}
      </div>

      {/* Barra de herramientas */}
      {!vistaPrevia && (
        <div className="flex flex-wrap items-center gap-1 rounded-lg border border-soft p-2">
          <button onClick={() => agregar('titulo')} className="btn-secondary !px-2 !py-1 text-xs"><Type size={12} /> Título</button>
          <button onClick={() => agregar('texto')} className="btn-secondary !px-2 !py-1 text-xs"><Type size={12} /> Texto</button>
          <label className="btn-secondary !px-2 !py-1 text-xs cursor-pointer flex items-center gap-1">
            <ImageIcon size={12} /> Logo/Imagen
            <input type="file" accept="image/*" className="hidden" onChange={(e) => {
              const f = e.target.files?.[0];
              if (f) agregarImagen(f);
              e.currentTarget.value = '';
            }} />
          </label>
          <button onClick={() => agregar('barcode')} className="btn-secondary !px-2 !py-1 text-xs"><Type size={12} /> Código de barras</button>
          <button onClick={() => agregar('linea')} className="btn-secondary !px-2 !py-1 text-xs">— Línea</button>
          <button onClick={eliminarSeleccion} className="btn-danger !px-2 !py-1 text-xs" disabled={!seleccion}>Eliminar</button>
          <span className="ml-2 text-[10px] text-ink-muted">
            Arrastrá los elementos sobre la hoja · {interpolar('{fecha_hoy}')} disponible como {'{fecha_hoy}'} y {'{nombre_reporte}'}
          </span>
        </div>
      )}

      <div className="grid grid-cols-1 gap-3 lg:grid-cols-[1fr_260px]">
        {/* Hoja */}
        <div className="overflow-auto rounded-lg border border-soft bg-black/5 p-4" style={{ maxHeight: 700 }}>
          <div
            className="relative bg-white shadow-lg"
            style={{ width: ANCHO_HOJA, height: ALTO_HOJA, transformOrigin: 'top left' }}
            onPointerDown={() => !vistaPrevia && setSeleccion(null)}
          >
            {cargando ? (
              <div className="flex h-full items-center justify-center text-sm text-black/50">
                <Loader2 className="mr-2 animate-spin" size={16} /> Ejecutando reporte…
              </div>
            ) : (
              <>
                {diseno.elementos.map((el) => (
                  el.tipo === 'tabla' ? (
                    <div
                      key={el.id}
                      onPointerDown={(e) => !vistaPrevia && (e.stopPropagation(), setSeleccion(el.id))}
                      style={{
                        position: 'absolute', left: el.x, top: el.y, width: el.ancho, height: el.alto,
                        overflow: 'auto', outline: !vistaPrevia && seleccion === el.id ? '1px dashed #2563eb' : undefined,
                        cursor: vistaPrevia ? 'default' : 'move'
                      }}
                    >
                      {resultado && <TablaResultado resultado={resultado} def={def} reporteId={reporte.id} />}
                    </div>
                  ) : (
                    <ElementoCanvas
                      key={el.id}
                      el={el}
                      seleccionado={seleccion === el.id}
                      onSeleccionar={() => setSeleccion(el.id)}
                      onMover={(x, y) => actualizar(el.id, { x, y })}
                      onRedimensionar={(ancho, alto) => actualizar(el.id, { ancho, alto })}
                      modoEdicion={!vistaPrevia}
                    />
                  )
                ))}
              </>
            )}
          </div>
        </div>

        {/* Panel de propiedades */}
        <div className="card space-y-2 text-xs">
          <h3 className="text-sm font-semibold">Propiedades</h3>
          {!seleccionado ? (
            <p className="text-xs text-ink-secondary">Seleccioná un elemento en la hoja para editar sus propiedades.</p>
          ) : (
            <div className="space-y-2 text-xs">
              <p className="badge bg-black/5">tipo: {seleccionado.tipo}</p>
              <div className="grid grid-cols-2 gap-2">
                {(['x', 'y', 'ancho', 'alto'] as const).map((k) => (
                  <label key={k} className="flex flex-col">
                    {k}
                    <input
                      type="number"
                      value={seleccionado[k]}
                      onChange={(e) => actualizar(seleccionado.id, { [k]: Number(e.target.value) })}
                      className="input !py-0.5"
                    />
                  </label>
                ))}
              </div>
              {(seleccionado.tipo === 'titulo' || seleccionado.tipo === 'texto') && (
                <>
                  <label className="block">
                    Texto (admite {'{fecha_hoy}'} y {'{nombre_reporte}'})
                    <textarea
                      value={seleccionado.texto ?? ''}
                      onChange={(e) => actualizar(seleccionado.id, { texto: e.target.value })}
                      rows={2}
                      className="input w-full"
                    />
                  </label>
                  <div className="flex flex-wrap items-center gap-2">
                    <label className="flex items-center gap-1">
                      tamaño
                      <input type="number" min={8} max={60} value={seleccionado.tamano ?? 12}
                        onChange={(e) => actualizar(seleccionado.id, { tamano: Number(e.target.value) })}
                        className="input !py-0.5 w-14" />
                    </label>
                    <button onClick={() => actualizar(seleccionado.id, { negrita: !seleccionado.negrita })}
                      className={seleccionado.negrita ? 'bg-blue-100 rounded p-1' : 'rounded p-1'}><Bold size={13} /></button>
                    <button onClick={() => actualizar(seleccionado.id, { italica: !seleccionado.italica })}
                      className={seleccionado.italica ? 'bg-blue-100 rounded p-1' : 'rounded p-1'}><Italic size={13} /></button>
                    <button onClick={() => actualizar(seleccionado.id, { subrayado: !seleccionado.subrayado })}
                      className={seleccionado.subrayado ? 'bg-blue-100 rounded p-1' : 'rounded p-1'}><Underline size={13} /></button>
                  </div>
                  <div className="flex items-center gap-2">
                    <button onClick={() => actualizar(seleccionado.id, { alineacion: 'izq' })}
                      className={seleccionado.alineacion === 'izq' ? 'bg-blue-100 rounded p-1' : 'rounded p-1'}><AlignLeft size={13} /></button>
                    <button onClick={() => actualizar(seleccionado.id, { alineacion: 'centro' })}
                      className={seleccionado.alineacion === 'centro' ? 'bg-blue-100 rounded p-1' : 'rounded p-1'}><AlignCenter size={13} /></button>
                    <button onClick={() => actualizar(seleccionado.id, { alineacion: 'der' })}
                      className={seleccionado.alineacion === 'der' ? 'bg-blue-100 rounded p-1' : 'rounded p-1'}><AlignRight size={13} /></button>
                    <input type="color" value={seleccionado.color ?? '#000000'}
                      onChange={(e) => actualizar(seleccionado.id, { color: e.target.value })}
                      className="h-7 w-10 cursor-pointer" title="Color del texto" />
                  </div>
                </>
              )}
              {seleccionado.tipo === 'imagen' && (
                <label className="btn-secondary block cursor-pointer text-center text-xs">
                  Cambiar imagen
                  <input type="file" accept="image/*" className="hidden" onChange={(e) => {
                    const f = e.target.files?.[0];
                    if (!f) return;
                    const lector = new FileReader();
                    lector.onload = () => actualizar(seleccionado.id, { imagen: String(lector.result) });
                    lector.readAsDataURL(f);
                  }} />
                </label>
              )}
              {seleccionado.tipo === 'barcode' && (
                <>
                  <label className="block">
                    Valor (admite {'{fecha_hoy}'} y {'{nombre_reporte}'})
                    <input value={seleccionado.valor ?? ''} onChange={(e) => actualizar(seleccionado.id, { valor: e.target.value })}
                      className="input w-full" />
                  </label>
                  <label className="flex items-center gap-1">
                    <input type="checkbox" checked={seleccionado.mostrarTexto ?? false}
                      onChange={(e) => actualizar(seleccionado.id, { mostrarTexto: e.target.checked })} />
                    mostrar el valor debajo
                  </label>
                </>
              )}
              <p className="text-[10px] text-ink-muted">
                {seleccionado.tipo === 'tabla'
                  ? 'Este elemento contiene el reporte. Redimensionalo con el punto azul de la esquina.'
                  : 'Arrastralo sobre la hoja; el punto azul de la esquina lo redimensiona.'}
              </p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}


/** Hoja A4 de solo lectura con el diseño guardado (usada por el visor). */
export function HojaReporte({ diseno, resultado, def, nombre, reporteId }: {
  diseno: ReporteDiseno;
  resultado: ResultadoReporteDto | null;
  def: ReporteCompletoDto['definicion'];
  nombre: string;
  reporteId?: string;
}) {
  reporteNombreGlobal = nombre;
  return (
    <div className="overflow-auto rounded-lg border border-soft bg-black/5 p-4 dark:bg-black/20" style={{ maxHeight: 700 }}>
      <div className="relative bg-white shadow-lg" style={{ width: ANCHO_HOJA, height: ALTO_HOJA }}>
        {diseno.elementos.map((el) => (
          el.tipo === 'tabla' ? (
            <div key={el.id} style={{ position: 'absolute', left: el.x, top: el.y, width: el.ancho, height: el.alto, overflow: 'auto' }}>
              {resultado
                ? <TablaResultado resultado={resultado} def={def} reporteId={reporteId} />
                : <p className="text-[10px] text-black/40">Sin datos todavía.</p>}
            </div>
          ) : (
            <ElementoCanvas
              key={el.id}
              el={el}
              seleccionado={false}
              onSeleccionar={() => {}}
              onMover={() => {}}
              onRedimensionar={() => {}}
              modoEdicion={false}
            />
          )
        ))}
      </div>
    </div>
  );
}
