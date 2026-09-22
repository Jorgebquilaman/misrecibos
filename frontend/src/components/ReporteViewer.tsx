import { useEffect, useMemo, useRef, useState } from 'react';
import {
  Bar, BarChart, CartesianGrid, Cell, Legend, Line, LineChart, Pie, PieChart,
  ResponsiveContainer, Tooltip, XAxis, YAxis
} from 'recharts';
import { ChevronDown, ChevronRight, Download, FileSpreadsheet, Loader2 } from 'lucide-react';

import { reportesApi } from '../api';
import { HojaReporte } from './ReporteCanvas';
import { exportarReporteAExcel, exportarReporteAPdf } from '../utils/exportarReporte';
import type { ReporteDiseno } from '../types';
import type {
  ReporteCompletoDto, ResultadoReporteDto, CampoFilasDef, CampoValorDef, TipoDatoReporte
} from '../types';

const COLORES_TORTA = ['#2563eb', '#16a34a', '#ea580c', '#9333ea', '#0891b2', '#dc2626', '#ca8a04', '#0d9488'];

function formatear(valor: unknown, tipo: TipoDatoReporte | 'Numero'): string {
  if (valor === null || valor === undefined) return '—';
  if (tipo === 'Numero' || typeof valor === 'number') {
    const n = typeof valor === 'number' ? valor : Number(valor);
    return isNaN(n) ? String(valor) : n.toLocaleString('es-AR', { maximumFractionDigits: 2 });
  }
  if (valor instanceof Date) return valor.toLocaleString('es-AR');
  return String(valor);
}

const esNumero = (v: unknown) => typeof v === 'number' && !isNaN(v);

/** Subtotal por grupo: suma/conteo acumulan, max/min comparan, promedio promedia las hojas. */
function subtotal(valores: CampoValorDef[], hojas: Record<string, unknown>[], alias: string): number | null {
  const vals = hojas.map((f) => f[alias]).filter(esNumero) as number[];
  if (vals.length === 0) return null;
  const def = valores.find((v) => v.alias === alias);
  switch (def?.agregacion) {
    case 'Maximo': return Math.max(...vals);
    case 'Minimo': return Math.min(...vals);
    case 'Promedio': return vals.reduce((a, b) => a + b, 0) / vals.length;
    default: return vals.reduce((a, b) => a + b, 0);
  }
}

interface NodoGrupo {
  clave: string;
  nivel: number;
  hijos: NodoGrupo[];
  filasHojas: Record<string, unknown>[];
  valoresNivel: Record<string, unknown>;
}

function ArmarArbol(
  filasResultado: Record<string, unknown>[],
  niveles: CampoFilasDef[]
): NodoGrupo[] {
  const raiz: NodoGrupo[] = [];
  for (const fila of filasResultado) {
    let hermanos = raiz;
    for (let nivel = 0; nivel < niveles.length; nivel++) {
      const clave = String(fila[niveles[nivel].alias] ?? '—');
      let nodo = hermanos.find((h) => h.clave === clave && h.nivel === nivel);
      if (!nodo) {
        nodo = { clave, nivel, hijos: [], filasHojas: [], valoresNivel: fila };
        hermanos.push(nodo);
      }
      if (nivel === niveles.length - 1) nodo.filasHojas.push(fila);
      hermanos = nodo.hijos;
    }
  }
  // Los grupos con un solo nivel guardan su fila agregada como hoja propia.
  if (niveles.length === 1) {
    for (const nodo of raiz) if (nodo.filasHojas.length === 0) nodo.filasHojas.push(nodo.valoresNivel);
  }
  return raiz;
}

function FilaGrupo({
  nodo, niveles, valores, abiertos, toggle, profundidad, onExpandirSub, indiceSub, reporteId,
  conAgregaciones, clavesDetalle
}: {
  nodo: NodoGrupo;
  niveles: CampoFilasDef[];
  valores: CampoValorDef[];
  abiertos: Set<string>;
  toggle: (k: string) => void;
  profundidad: number;
  onExpandirSub?: (nodo: NodoGrupo) => void;
  indiceSub?: number;
  reporteId?: string;
  conAgregaciones: boolean;
  clavesDetalle: string[];
}) {
  const id = `${nodo.nivel}:${nodo.clave}`;
  const abierto = abiertos.has(id);
  const tieneHijos = nodo.nivel < niveles.length - 1;

  return (
    <>
      <tr className="border-b border-soft bg-black/[0.03]">
        <td className="py-1.5" style={{ paddingLeft: `${(profundidad + 1) * 20}px` }}>
          <button onClick={() => toggle(id)} className="flex items-center gap-1 font-medium hover:text-blue-600">
            {abierto ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
            {nodo.clave}
            {!conAgregaciones && nodo.filasHojas.length > 0 && (
              <span className="text-[10px] text-ink-muted">({nodo.filasHojas.length})</span>
            )}
          </button>
        </td>
        {valores.map((v) => (
          <td key={v.alias} className="py-1.5 pr-3 text-right font-mono text-xs">
            {formatear(subtotal(valores, nodo.filasHojas, v.alias) ?? nodo.valoresNivel[v.alias], 'Numero')}
          </td>
        ))}
        {indiceSub !== undefined && onExpandirSub && (
          <td className="py-1.5 pr-2 text-right">
            <button
              onClick={() => onExpandirSub(nodo)}
              className="text-xs font-medium text-blue-600 hover:underline"
              title="Ver detalle"
            >
              detalle
            </button>
          </td>
        )}
      </tr>
      {abierto && (
        tieneHijos
          ? nodo.hijos.map((h) => (
              <FilaGrupo key={`${h.nivel}:${h.clave}`} nodo={h} niveles={niveles} valores={valores}
                abiertos={abiertos} toggle={toggle} profundidad={profundidad + 1}
                onExpandirSub={onExpandirSub} indiceSub={indiceSub} reporteId={reporteId}
                conAgregaciones={conAgregaciones} clavesDetalle={clavesDetalle} />
            ))
          : conAgregaciones && reporteId ? (
              <DetalleGrupo reporteId={reporteId} nodo={nodo} valores={valores}
                indiceSub={indiceSub} profundidad={profundidad} />
            ) : (
              nodo.filasHojas.map((f, i) => (
                <tr key={i} className="border-b border-soft last:border-0">
                  <td className="py-1 text-xs text-ink-muted" style={{ paddingLeft: `${(profundidad + 2) * 20}px` }} />
                  {clavesDetalle.map((c) => (
                    <td key={c} className="py-1 pr-3">{formatear(f[c], 'Texto')}</td>
                  ))}
                  {indiceSub !== undefined && onExpandirSub && <td />}
                </tr>
              ))
            )
      )}
    </>
  );
}

function DetalleGrupo({ reporteId, nodo, valores, indiceSub, profundidad }: {
  reporteId: string;
  nodo: NodoGrupo;
  valores: CampoValorDef[];
  indiceSub?: number;
  profundidad: number;
}) {
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [detalle, setDetalle] = useState<ResultadoReporteDto | null>(null);

  useEffect(() => {
    const valoresFila: Record<string, unknown> = {};
    // Los valores del nodo (agregado) contienen las claves de todos los niveles hasta este nodo.
    for (const [k, v] of Object.entries(nodo.valoresNivel)) valoresFila[k] = v;
    reportesApi.detalle(reporteId, valoresFila)
      .then(setDetalle)
      .catch((e) => setError(e.response?.data?.error ?? 'Error al traer el detalle'))
      .finally(() => setCargando(false));
  }, [reporteId, nodo]);

  if (cargando)
    return (
      <tr><td colSpan={valores.length + 2} className="py-1 pl-10 text-xs text-ink-secondary">
        Cargando detalle…</td></tr>
    );
  if (error) return <tr><td colSpan={valores.length + 2} className="py-1 text-xs text-danger">{error}</td></tr>;
  if (!detalle || detalle.filas.length === 0)
    return <tr><td colSpan={valores.length + 2} className="py-1 pl-10 text-xs text-ink-secondary">Sin filas de detalle.</td></tr>;

  return (
    <tr>
      <td colSpan={valores.length + (indiceSub !== undefined ? 2 : 1)} className="bg-black/[0.02] p-2" style={{ paddingLeft: `${(profundidad + 2) * 20}px` }}>
        <div className="overflow-x-auto rounded border border-soft bg-white">
          <table className="w-full text-left text-[11px]">
            <thead>
              <tr className="border-b border-soft text-[10px] uppercase tracking-wide text-ink-muted">
                {detalle.columnas.map((c) => <th key={c} className="px-2 py-1">{c}</th>)}
              </tr>
            </thead>
            <tbody>
              {detalle.filas.slice(0, 200).map((f, i) => (
                <tr key={i} className="border-b border-soft last:border-0">
                  {detalle.columnas.map((c) => (
                    <td key={c} className="px-2 py-1">{formatear(f[c], 'Texto')}</td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
          {detalle.filas.length > 200 && (
            <p className="px-2 py-1 text-[10px] text-ink-muted">Mostrando las primeras 200 filas de {detalle.filas.length}.</p>
          )}
        </div>
      </td>
    </tr>
  );
}

function GrillaSubreporte({ reportePadreId, indice, valoresFila }: {
  reportePadreId: string; indice: number; valoresFila: Record<string, unknown>;
}) {
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [resultado, setResultado] = useState<ResultadoReporteDto | null>(null);

  useEffect(() => {
    setCargando(true);
    reportesApi.ejecutarSubreporte(reportePadreId, indice, valoresFila, {})
      .then(setResultado)
      .catch((e) => setError(e.response?.data?.error ?? 'Error al ejecutar el subreporte'))
      .finally(() => setCargando(false));
  }, [reportePadreId, indice, valoresFila]);

  if (cargando) return <p className="flex items-center gap-2 py-2 text-xs text-ink-secondary"><Loader2 size={12} className="animate-spin" /> Cargando detalle…</p>;
  if (error) return <p className="py-2 text-xs text-danger">{error}</p>;
  if (!resultado || resultado.filas.length === 0) return <p className="py-2 text-xs text-ink-secondary">Sin datos.</p>;

  return (
    <div className="my-1 overflow-x-auto rounded border border-soft">
      <table className="w-full text-left text-xs">
        <thead>
          <tr className="border-b border-soft bg-black/[0.02] text-[10px] uppercase tracking-wide text-ink-muted">
            {resultado.columnas.map((c) => <th key={c} className="py-1 pr-3">{c}</th>)}
          </tr>
        </thead>
        <tbody>
          {resultado.filas.map((f, i) => (
            <tr key={i} className="border-b border-soft last:border-0">
              {resultado.columnas.map((c) => <td key={c} className="py-1 pr-3">{formatear(f[c], 'Texto')}</td>)}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function EncabezadoOrdenable({ texto, col, orden, onToggle, alineadoDerecha }: {
  texto: string;
  col: string;
  orden: { col: string; dir: 'asc' | 'desc' } | null;
  onToggle: (c: string) => void;
  alineadoDerecha?: boolean;
}) {
  const activo = orden?.col === col;
  return (
    <th className={`py-2 pr-3 ${alineadoDerecha ? 'text-right' : ''}`}>
      <button onClick={() => onToggle(col)} className="flex items-center gap-0.5 uppercase tracking-wide hover:text-blue-600" title="Ordenar por esta columna">
        {texto}
        <span className="text-[10px]">
          {activo ? (orden!.dir === 'asc' ? '▲' : '▼') : <span className="opacity-30">⇅</span>}
        </span>
      </button>
    </th>
  );
}

export default function ReporteViewer({ reporte }: { reporte: ReporteCompletoDto }) {
  const def = reporte.definicion;
  const [valores, setValores] = useState<Record<string, string>>({});
  const [ejecutando, setEjecutando] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [resultado, setResultado] = useState<ResultadoReporteDto | null>(null);
  const [abiertos, setAbiertos] = useState<Set<string>>(new Set());
  const [exportando, setExportando] = useState(false);
  const [verTabla, setVerTabla] = useState(false);
  const [orden, setOrden] = useState<{ col: string; dir: 'asc' | 'desc' } | null>(null);

  const toggleOrden = (col: string) =>
    setOrden((prev) => (prev?.col === col ? (prev.dir === 'asc' ? { col, dir: 'desc' } : null) : { col, dir: 'asc' }));
  const [subAbierto, setSubAbierto] = useState<{ indice: number; valoresFila: Record<string, unknown>; titulo: string } | null>(null);

  const filtrosParam = useMemo(() => def.filtros.filter((f) => f.parametrizable), [def]);

  const filasOrdenadas = useMemo(() => {
    if (!resultado || !orden) return resultado?.filas ?? [];
    const { col, dir } = orden;
    return [...resultado.filas].sort((a, b) => {
      const va = a[col], vb = b[col];
      if (va === null || va === undefined) return 1;
      if (vb === null || vb === undefined) return -1;
      if (typeof va === 'number' && typeof vb === 'number') return dir === 'asc' ? va - vb : vb - va;
      const ca = String(va), cb = String(vb);
      return dir === 'asc' ? ca.localeCompare(cb, 'es') : cb.localeCompare(ca, 'es');
    });
  }, [resultado, orden]);
  const toggle = (k: string) =>
    setAbiertos((prev) => {
      const next = new Set(prev);
      next.has(k) ? next.delete(k) : next.add(k);
      return next;
    });

  // Si el reporte no tiene filtros parametrizables, lo ejecutamos al abrir.
  const autoEjecutado = useRef(false);
  useEffect(() => {
    if (filtrosParam.length > 0 || autoEjecutado.current) return;
    autoEjecutado.current = true;
    reportesApi.ejecutar(reporte.id, {})
      .then((res) => {
        setResultado(res);
        if (def.filas.length > 0) {
          const primero = new Set<string>();
          for (const f of res.filas) primero.add(`0:${String(f[def.filas[0].alias] ?? '—')}`);
          setAbiertos(primero);
        }
      })
      .catch((e) => setError(e.response?.data?.error ?? 'Error al ejecutar el reporte'));
  }, [filtrosParam.length, reporte.id, def.filas]);

  const ejecutar = async () => {
    setEjecutando(true);
    setError(null);
    setSubAbierto(null);
    try {
      const payload: Record<string, unknown> = {};
      for (const f of filtrosParam) {
        const v = valores[f.campo];
        if (v === undefined || v === '') continue;
        payload[f.campo] = f.tipoDato === 'Numero' ? Number(v) : v;
        if (f.operador === 'Entre' && valores[f.campo + '_hasta'])
          payload[f.campo + '_hasta'] = f.tipoDato === 'Numero' ? Number(valores[f.campo + '_hasta']) : valores[f.campo + '_hasta'];
        if (f.operador === 'En')
          payload[f.campo] = v.split(',').map((s) => s.trim()).filter(Boolean);
      }
      setResultado(await reportesApi.ejecutar(reporte.id, payload));
    } catch (e: unknown) {
      const err = e as { response?: { data?: { error?: string } } };
      setError(err.response?.data?.error ?? 'Error al ejecutar el reporte');
    } finally {
      setEjecutando(false);
    }
  };

  const abrirTodo = () => {
    if (!resultado || def.filas.length === 0) return;
    const todos = new Set<string>();
    for (const f of resultado.filas) todos.add(`0:${String(f[def.filas[0].alias] ?? '—')}`);
    setAbiertos(todos);
  };

  const haySubreportes = def.subreportes.length > 0;

  const diseno = useMemo(() => {
    try {
      const parseado = JSON.parse(reporte.disenoJson) as ReporteDiseno;
      return parseado?.elementos?.length ? parseado : null;
    } catch { return null; }
  }, [reporte.disenoJson]);

  const exportarXlsx = () => { if (resultado) return exportarReporteAExcel(reporte, resultado); };
  const exportarPdf = () => { if (resultado) return exportarReporteAPdf(reporte, resultado); };


  return (
    <div className="space-y-4">
      {filtrosParam.length > 0 && (
        <div className="card space-y-3">
          <h3 className="text-sm font-semibold">Filtros</h3>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            {filtrosParam.map((f, i) => (
              <label key={f.campo + i} className="text-xs">
                <span className="mb-1 block font-medium text-ink-secondary">
                  {f.etiqueta ?? f.campo}
                  {f.operador === 'Entre' && ' (desde)'}
                </span>
                <input
                  type={f.tipoDato === 'Numero' ? 'number' : f.tipoDato === 'Fecha' ? 'datetime-local' : 'text'}
                  value={valores[f.campo] ?? ''}
                  onChange={(e) => setValores((v) => ({ ...v, [f.campo]: e.target.value }))}
                  className="input w-full"
                />
                {f.operador === 'Entre' && (
                  <>
                    <span className="mb-1 mt-2 block font-medium text-ink-secondary">hasta</span>
                    <input
                      type={f.tipoDato === 'Numero' ? 'number' : f.tipoDato === 'Fecha' ? 'datetime-local' : 'text'}
                      value={valores[f.campo + '_hasta'] ?? ''}
                      onChange={(e) => setValores((v) => ({ ...v, [f.campo + '_hasta']: e.target.value }))}
                      className="input w-full"
                    />
                  </>
                )}
                {f.operador === 'En' && (
                  <span className="mt-1 block text-[10px] text-ink-muted">Valores separados por coma</span>
                )}
              </label>
            ))}
          </div>
          <button onClick={ejecutar} disabled={ejecutando} className="btn-primary flex items-center gap-2">
            {ejecutando && <Loader2 size={14} className="animate-spin" />} Ejecutar
          </button>
        </div>
      )}

      {error && <p className="text-sm text-danger">{error}</p>}

      {def.graficos.length > 0 && resultado && (
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          {def.graficos.map((g, i) => (
            <div key={i} className="card">
              <h3 className="mb-2 text-sm font-semibold">{g.titulo}</h3>
              <div style={{ height: 260 }}>
                <ResponsiveContainer width="100%" height="100%">
                  {g.tipo === 'Torta' ? (
                    <PieChart>
                      <Pie data={resultado.filas} dataKey={g.camposY[0]} nameKey={g.campoX}
                        outerRadius={90} label>
                        {resultado.filas.map((_, i) => <Cell key={i} fill={COLORES_TORTA[i % COLORES_TORTA.length]} />)}
                      </Pie>
                      <Tooltip />
                      <Legend />
                    </PieChart>
                  ) : g.tipo === 'Lineas' ? (
                    <LineChart data={resultado.filas}>
                      <CartesianGrid strokeDasharray="3 3" opacity={0.3} />
                      <XAxis dataKey={g.campoX} fontSize={11} />
                      <YAxis fontSize={11} />
                      <Tooltip />
                      <Legend />
                      {g.camposY.map((y, i) => (
                        <Line key={y} type="monotone" dataKey={y} stroke={COLORES_TORTA[i % COLORES_TORTA.length]} />
                      ))}
                    </LineChart>
                  ) : (
                    <BarChart data={resultado.filas}>
                      <CartesianGrid strokeDasharray="3 3" opacity={0.3} />
                      <XAxis dataKey={g.campoX} fontSize={11} />
                      <YAxis fontSize={11} />
                      <Tooltip />
                      <Legend />
                      {g.camposY.map((y, i) => (
                        <Bar key={y} dataKey={y} fill={COLORES_TORTA[i % COLORES_TORTA.length]} />
                      ))}
                    </BarChart>
                  )}
                </ResponsiveContainer>
              </div>
            </div>
          ))}
        </div>
      )}

      {diseno && resultado && !verTabla && (
        <div className="space-y-2">
          <div className="flex items-center gap-1">
            <button onClick={() => setVerTabla(true)} className="btn-secondary !px-2 !py-1 text-xs">
              Ver tabla interactiva
            </button>
            <button onClick={async () => { setExportando(true); try { await exportarXlsx(); } finally { setExportando(false); } }} disabled={exportando} className="btn-secondary !px-2 !py-1 text-xs">
              {exportando ? <Loader2 size={13} className="animate-spin" /> : <FileSpreadsheet size={13} />} XLSX
            </button>
            <button onClick={async () => { setExportando(true); try { await exportarPdf(); } finally { setExportando(false); } }} disabled={exportando} className="btn-secondary !px-2 !py-1 text-xs">
              {exportando ? <Loader2 size={13} className="animate-spin" /> : <Download size={13} />} PDF
            </button>
          </div>
          <HojaReporte diseno={diseno} resultado={resultado} def={def} nombre={reporte.nombre} reporteId={reporte.id} />
        </div>
      )}

      {resultado && (!diseno || verTabla) && (
        <div className="card">
          <div className="mb-2 flex items-center justify-between">
            <h3 className="text-sm font-semibold">
              Resultado ({resultado.filas.length} {def.filas.length > 0 ? 'grupos' : 'filas'})
            </h3>
            <div className="flex items-center gap-1">
              {diseno && (
                <button onClick={() => setVerTabla(!verTabla)} className="btn-secondary !px-2 !py-1 text-xs">
                  {verTabla ? 'Ver hoja diseñada' : 'Ver tabla interactiva'}
                </button>
              )}
              <button onClick={async () => { setExportando(true); try { await exportarXlsx(); } finally { setExportando(false); } }} disabled={exportando} className="btn-secondary !px-2 !py-1 text-xs" title="Exportar a Excel">
                {exportando ? <Loader2 size={13} className="animate-spin" /> : <FileSpreadsheet size={13} />} XLSX
              </button>
              <button onClick={async () => { setExportando(true); try { await exportarPdf(); } finally { setExportando(false); } }} disabled={exportando} className="btn-secondary !px-2 !py-1 text-xs" title="Exportar a PDF">
                {exportando ? <Loader2 size={13} className="animate-spin" /> : <Download size={13} />} PDF
              </button>
              {def.filas.length > 1 && (
                <button onClick={abrirTodo} className="btn-secondary !px-2 !py-1 text-xs">Abrir primer nivel</button>
              )}
            </div>
          </div>
          {resultado.filas.length === 0 ? (
            <p className="text-sm text-ink-secondary">Sin datos para los filtros aplicados.</p>
          ) : def.filas.length > 0 ? (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm">
                <thead>
                  <tr className="border-b border-soft text-xs uppercase tracking-wide text-ink-muted">
                    <EncabezadoOrdenable texto="Grupo" col={def.filas[0].alias} orden={orden} onToggle={toggleOrden} />
                    {def.valores.length > 0
                      ? def.valores.map((v) => (
                          <EncabezadoOrdenable key={v.alias} texto={v.alias} col={v.alias} orden={orden} onToggle={toggleOrden} alineadoDerecha />
                        ))
                      : (resultado.columnas.filter((c) => !def.filas.some((n) => n.alias === c))).map((c) => (
                          <EncabezadoOrdenable key={c} texto={c} col={c} orden={orden} onToggle={toggleOrden} />
                        ))}
                    {haySubreportes && <th className="py-2 pr-2" />}
                  </tr>
                </thead>
                <tbody>
                  <ArbolResultado resultado={resultado} def={def} abiertos={abiertos} toggle={toggle}
                    reporteId={reporte.id} subAbierto={subAbierto} setSubAbierto={setSubAbierto}
                    filasOrdenadas={filasOrdenadas} />
                </tbody>
                <tfoot>
                  <tr className="border-t-2 border-soft font-semibold">
                    <td className="py-2">Total general</td>
                    {def.valores.map((v) => (
                      <td key={v.alias} className="py-2 pr-3 text-right font-mono text-xs">
                        {formatear(resultado.totales?.[v.alias], 'Numero')}
                      </td>
                    ))}
                    {haySubreportes && <td />}
                  </tr>
                </tfoot>
              </table>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm">
                <thead>
                  <tr className="border-b border-soft text-xs uppercase tracking-wide text-ink-muted">
                    {resultado.columnas.map((c) => (
                      <EncabezadoOrdenable key={c} texto={c} col={c} orden={orden} onToggle={toggleOrden} />
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {filasOrdenadas.map((f, i) => (
                    <tr key={i} className="border-b border-soft last:border-0">
                      {resultado.columnas.map((c) => <td key={c} className="py-1.5 pr-3">{formatear(f[c], 'Texto')}</td>)}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

/** Renderiza el árbol de grupos + el panel del subreporte expandido. */
function ArbolResultado({
  resultado, def, abiertos, toggle, reporteId, subAbierto, setSubAbierto, filasOrdenadas
}: {
  resultado: ResultadoReporteDto;
  def: ReporteCompletoDto['definicion'];
  abiertos: Set<string>;
  toggle: (k: string) => void;
  reporteId: string;
  subAbierto: { indice: number; valoresFila: Record<string, unknown>; titulo: string } | null;
  setSubAbierto: (s: { indice: number; valoresFila: Record<string, unknown>; titulo: string } | null) => void;
  filasOrdenadas: Record<string, unknown>[];
}) {
  const arbol = useMemo(() => ArmarArbol(filasOrdenadas, def.filas), [filasOrdenadas, def]);
  const conAgregaciones = def.valores.length > 0;
  const clavesDetalle = resultado.columnas.filter((c) => !def.filas.some((n) => n.alias === c));

  const expandirSub = (indice: number) => (nodo: NodoGrupo) => {
    const valoresFila: Record<string, unknown> = {};
    // Los valores disponibles de la fila son los de la fila agregada del grupo.
    for (const [k, v] of Object.entries(nodo.valoresNivel)) valoresFila[k] = v;
    setSubAbierto({ indice, valoresFila, titulo: nodo.clave });
  };

  return (
    <>
      {arbol.map((nodo) => (
        <FilaGrupo
          key={`${nodo.nivel}:${nodo.clave}`}
          nodo={nodo}
          niveles={def.filas}
          valores={def.valores}
          abiertos={abiertos}
          toggle={toggle}
          profundidad={0}
          onExpandirSub={def.subreportes.length > 0 ? expandirSub(0) : undefined}
          indiceSub={def.subreportes.length > 0 ? 0 : undefined}
          reporteId={reporteId}
          conAgregaciones={conAgregaciones}
          clavesDetalle={clavesDetalle}
        />
      ))}
      {subAbierto && (
        <tr>
          <td colSpan={def.valores.length + 2} className="bg-black/[0.02] p-2 pl-10">
            <div className="rounded border border-soft p-2">
              <p className="mb-1 text-xs font-semibold">
                Subreporte: {def.subreportes[subAbierto.indice]?.nombre ?? 'Detalle'} — {subAbierto.titulo}
              </p>
              <GrillaSubreporte reportePadreId={reporteId} indice={subAbierto.indice} valoresFila={subAbierto.valoresFila} />
              <button onClick={() => setSubAbierto(null)} className="mt-1 text-xs text-ink-secondary hover:underline">
                cerrar
              </button>
            </div>
          </td>
        </tr>
      )}
    </>
  );
}
