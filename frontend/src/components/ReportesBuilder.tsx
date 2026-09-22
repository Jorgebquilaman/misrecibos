import { useEffect, useState } from 'react';
import {
  DndContext, PointerSensor, useDraggable, useDroppable, useSensor, useSensors,
  type DragEndEvent
} from '@dnd-kit/core';
import { ArrowDown, ArrowUp, BarChart3, Loader2, Plus, Save, Search, Table2, Wand2, X } from 'lucide-react';
import { reportesApi } from '../api';
import AsistenteVisual from './AsistenteVisual';
import type {
  AgregacionReporte, ColumnaMetadata, OperadorFiltro, ReporteCompletoDto,
  ReporteDefinicionDto, ResultadoReporteDto, TipoDatoReporte, TipoGrafico
} from '../types';

const defVacia = (): ReporteDefinicionDto => ({
  filas: [], valores: [], columnas: [], filtros: [], graficos: [], subreportes: [], orden: null, limite: 5000
});

const OPERADORES_POR_TIPO: Record<TipoDatoReporte, OperadorFiltro[]> = {
  Texto: ['Igual', 'Distinto', 'Contiene', 'NoContiene', 'En', 'Vacio', 'NoVacio'],
  Numero: ['Igual', 'Distinto', 'Mayor', 'MayorIgual', 'Menor', 'MenorIgual', 'Entre', 'En'],
  Fecha: ['Igual', 'Mayor', 'MayorIgual', 'Menor', 'MenorIgual', 'Entre'],
  Booleano: ['Igual', 'Distinto']
};
const ETIQUETA_OPERADOR: Record<OperadorFiltro, string> = {
  Igual: 'igual a', Distinto: 'distinto de', Contiene: 'contiene', NoContiene: 'no contiene',
  En: 'en lista', Mayor: 'mayor a', MayorIgual: 'mayor o igual', Menor: 'menor a',
  MenorIgual: 'menor o igual', Entre: 'entre', Vacio: 'está vacío', NoVacio: 'no está vacío'
};

interface Props {
  reporte: ReporteCompletoDto | null;
  onGuardado: (id: string) => void;
  onCerrar: () => void;
}

export default function ReportesBuilder({ reporte, onGuardado, onCerrar }: Props) {
  const [nombre, setNombre] = useState(reporte?.nombre ?? '');
  const [descripcion, setDescripcion] = useState(reporte?.descripcion ?? '');
  const [querySql, setQuerySql] = useState(reporte?.querySql ?? '');
  const [definicion, setDefinicion] = useState<ReporteDefinicionDto>(reporte?.definicion ?? defVacia());
  const [id, setId] = useState<string | null>(reporte?.id ?? null);
  const [campos, setCampos] = useState<ColumnaMetadata[]>([]);
  const [guardando, setGuardando] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [subreportesDisponibles, setSubreportesDisponibles] = useState<{ id: string; nombre: string }[]>([]);
  const [conexiones, setConexiones] = useState<string[]>([]);
  const [conexion, setConexion] = useState(reporte?.conexion ?? 'PortalIUPA');
  const [preview, setPreview] = useState<ResultadoReporteDto | null>(null);
  const [cargandoPreview, setCargandoPreview] = useState(false);
  const [asistente, setAsistente] = useState(false);

  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 4 } }));

  useEffect(() => {
    reportesApi.conexiones().then(setConexiones).catch(() => {});
  }, []);

  useEffect(() => {
    if (!id) return;
    reportesApi.metadata(id).then(setCampos).catch(() => {});
    reportesApi.todos().then((todos) => setSubreportesDisponibles(
      todos.filter((r) => r.id !== id).map((r) => ({ id: r.id, nombre: r.nombre }))
    )).catch(() => {});
  }, [id]);

  const guardar = async (crear: boolean) => {
    if (!nombre.trim() || !querySql.trim()) {
      setError('Completá nombre y consulta SQL.');
      return;
    }
    setGuardando(true);
    setError(null);
    try {
      const res = crear
        ? await reportesApi.crear(nombre, descripcion || null, querySql, conexion, definicion)
        : await reportesApi.actualizar(id!, nombre, descripcion || null, querySql, conexion, definicion);
      setId(res.id);
      setCampos(await reportesApi.metadata(res.id));
      onGuardado(res.id);
    } catch (e: unknown) {
      const err = e as { response?: { data?: { error?: string } } };
      setError(err.response?.data?.error ?? 'Error al guardar el reporte');
    } finally {
      setGuardando(false);
    }
  };

  const onDragEnd = (e: DragEndEvent) => {
    const activo = String(e.active.id);
    const destino = e.over?.id ? String(e.over.id) : null;
    if (!destino || !activo.startsWith('campo:')) return;
    const [, campo, tipo] = activo.split(':');
    agregarCampo(destino, campo, tipo as TipoDatoReporte);
  };

  const agregarCampo = (zona: string, campo: string, tipo: TipoDatoReporte) => {
    setDefinicion((d) => {
      const aliasBase = campo;
      let alias = aliasBase;
      let n = 2;
      const usados = [...d.filas, ...d.valores, ...d.columnas].map((x) => x.alias);
      while (usados.includes(alias)) alias = `${aliasBase}_${n++}`;
      if (zona === 'zonas-filas' && d.filas.some((f) => f.campo === campo)) return d;
      if (zona === 'zonas-valores' && d.valores.some((v) => v.campo === campo)) return d;
      if (zona === 'zonas-columnas' && d.columnas.some((c) => c.campo === campo)) return d;
      if (zona === 'zonas-filtros' && d.filtros.some((f) => f.campo === campo)) return d;
      if (zona === 'zonas-filas')
        return { ...d, filas: [...d.filas, { campo, alias }] };
      if (zona === 'zonas-valores')
        return { ...d, valores: [...d.valores, { campo, alias, agregacion: 'Suma' as AgregacionReporte }] };
      if (zona === 'zonas-columnas')
        return { ...d, columnas: [...d.columnas, { campo, alias }] };
      if (zona === 'zonas-filtros')
        return {
          ...d,
          filtros: [...d.filtros, {
            campo, tipoDato: tipo, operador: 'Igual', etiqueta: campo,
            parametrizable: true, valor: null, valor2: null, valores: null
          }]
        };
      return d;
    });
  };

  const mover = <T,>(lista: T[], indice: number, delta: number): T[] => {
    const destino = indice + delta;
    if (destino < 0 || destino >= lista.length) return lista;
    const copia = [...lista];
    [copia[indice], copia[destino]] = [copia[destino], copia[indice]];
    return copia;
  };

  return (
    <DndContext sensors={sensors} onDragEnd={onDragEnd}>
      <div className="space-y-4">
        {/* Identidad del reporte */}
        <div className="card grid grid-cols-1 gap-3 sm:grid-cols-2">
          <label className="text-xs">
            <span className="mb-1 block font-medium text-ink-secondary">Nombre</span>
            <input value={nombre} onChange={(e) => setNombre(e.target.value)} className="input w-full" placeholder="Ej: Marcas por área" />
          </label>
          <label className="text-xs">
            <span className="mb-1 block font-medium text-ink-secondary">Descripción</span>
            <input value={descripcion} onChange={(e) => setDescripcion(e.target.value)} className="input w-full" />
          </label>
          <div className="text-xs">
            <span className="mb-1 block font-medium text-ink-secondary">Conexión (motor Postgres)</span>
            <div className="flex items-center gap-1">
              <select value={conexion} onChange={(e) => setConexion(e.target.value)} className="input flex-1">
                {conexiones.map((c) => <option key={c} value={c}>{c}</option>)}
              </select>
              <button
                onClick={() => setAsistente(true)}
                className="btn-secondary !px-2 !py-1 shrink-0"
                title="Constructor visual de la consulta: tablas, relaciones y campos"
                
              >
                <Table2 size={14} /> Constructor visual
              </button>
            </div>
            <span className="mt-1 block text-[10px] text-ink-muted">
              Las conexiones las define el administrador del servidor (appsettings → Reportes:Conexiones).
            </span>
          </div>
          <label className="text-xs sm:col-span-2">
            <span className="mb-1 block font-medium text-ink-secondary">Consulta SQL (solo SELECT)</span>
            <textarea
              value={querySql}
              onChange={(e) => setQuerySql(e.target.value)}
              rows={4}
              className="input w-full font-mono text-xs"
              placeholder={'SELECT area, legajo, fecha_hora FROM ...'}
            />
          </label>
        </div>

        {querySql.trim() && (
          <div className="card">
            <div className="flex flex-wrap items-center gap-2">
              <button
                onClick={async () => {
                  setCargandoPreview(true);
                  try {
                    setPreview(await reportesApi.previewSql(querySql, conexion));
                  } catch (e: unknown) {
                    const err = e as { response?: { data?: { error?: string } } };
                    setError(err.response?.data?.error ?? 'Error al ejecutar la consulta');
                  } finally {
                    setCargandoPreview(false);
                  }
                }}
                disabled={cargandoPreview}
                className="btn-secondary flex items-center gap-2 text-sm"
              >
                {cargandoPreview ? <Loader2 size={14} className="animate-spin" /> : <Search size={14} />}
                Ver resultado (ejemplo, top 100)
              </button>
              {/select\s+\*/i.test(querySql) && (
                <button
                  onClick={async () => {
                    setCargandoPreview(true);
                    try {
                      const res = await reportesApi.previewSql(querySql, conexion);
                      setPreview(res);
                      if (res.columnas.length === 0) {
                        setError('La consulta no devolvió columnas, no hay nada que expandir.');
                        return;
                      }
                      const columnasSql = res.columnas
                        .map((c) => /^[A-Za-z_][A-Za-z0-9_]*$/.test(c) ? c : `"${c}"`)
                        .join(', ');
                      setQuerySql(querySql.replace(/select\s+\*/i, `select ${columnasSql}`));
                      setError(null);
                    } catch (e: unknown) {
                      const err = e as { response?: { data?: { error?: string } } };
                      setError(err.response?.data?.error ?? 'Error al expandir el *');
                    } finally {
                      setCargandoPreview(false);
                    }
                  }}
                  disabled={cargandoPreview}
                  className="btn-secondary flex items-center gap-2 text-sm"
                  title="Ejecuta la consulta, toma los nombres reales de las columnas y reemplaza el *"
                >
                  <Wand2 size={14} /> Expandir * en columnas
                </button>
              )}
              {preview && (
                <span className="text-xs text-ink-secondary">
                  {preview.filas.length} filas · {preview.columnas.length} columnas
                </span>
              )}
            </div>
            {preview && (
              <div className="mt-2 max-h-72 overflow-auto rounded border border-soft">
                <table className="w-full text-left text-xs">
                  <thead>
                    <tr className="border-b border-soft bg-black/[0.02] text-[10px] uppercase tracking-wide text-ink-muted">
                      {preview.columnas.map((c) => <th key={c} className="px-2 py-1">{c}</th>)}
                    </tr>
                  </thead>
                  <tbody>
                    {preview.filas.map((f, i) => (
                      <tr key={i} className="border-b border-soft last:border-0">
                        {preview.columnas.map((c) => (
                          <td key={c} className="px-2 py-1 whitespace-nowrap">
                            {f[c] instanceof Date ? (f[c] as Date).toLocaleString('es-AR') : String(f[c] ?? '')}
                          </td>
                        ))}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}

        {error && <p className="text-sm text-danger">{error}</p>}

        {/* Panel de campos + zonas de drop */}
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-[280px_1fr]">
          <div className="relative">
          <div className="card absolute inset-0 flex flex-col overflow-hidden">
            <div className="mb-2 flex items-center justify-between gap-1">
              <h3 className="flex items-center gap-2 text-sm font-semibold">
                <Table2 size={15} /> Campos disponibles
              </h3>
              {querySql.trim() && (
                <button
                  onClick={async () => {
                    setCargandoPreview(true);
                    setError(null);
                    try {
                      setCampos(await reportesApi.metadataSql(querySql, conexion));
                    } catch (e: unknown) {
                      const err = e as { response?: { data?: { error?: string } } };
                      setError(err.response?.data?.error ?? 'Error al leer los campos de la consulta');
                    } finally {
                      setCargandoPreview(false);
                    }
                  }}
                  disabled={cargandoPreview}
                  className="btn-secondary !px-2 !py-0.5 text-[11px]"
                  title="Trae los campos de la consulta actual (sin guardar)"
                >
                  {cargandoPreview ? <Loader2 size={12} className="animate-spin" /> : <Search size={12} />} Actualizar
                </button>
              )}
            </div>
            {campos.length === 0 ? (
              <p className="text-xs text-ink-secondary">Guardá el reporte para cargar los campos de la consulta.</p>
            ) : (
              <ul className="flex-1 space-y-1 overflow-auto pr-1">
                {campos.map((c) => <CampoArrastrable key={c.nombre} nombre={c.nombre} tipo={c.tipo} />)}
              </ul>
            )}
          </div>
          </div>

          <div className="space-y-3">
            <ZonaDrop id="zonas-filas" titulo="Filas / Agrupamiento" vacio="Arrastrá campos para agrupar (nivel 1, 2, …)">
              {definicion.filas.map((f, i) => (
                <ItemZona key={f.campo} onQuitar={() => setDefinicion((d) => ({ ...d, filas: d.filas.filter((_, j) => j !== i) }))}
                  onSubir={() => setDefinicion((d) => ({ ...d, filas: mover(d.filas, i, -1) }))}
                  onBajar={() => setDefinicion((d) => ({ ...d, filas: mover(d.filas, i, 1) }))}>
                  <span className="text-xs font-medium">{f.campo}</span>
                  <input value={f.alias} onChange={(e) => setDefinicion((d) => ({
                    ...d, filas: d.filas.map((x, j) => j === i ? { ...x, alias: e.target.value } : x)
                  }))} className="input !py-0.5 !px-1 text-xs" />
                  <span className="text-[10px] text-ink-muted">nivel {i + 1}</span>
                </ItemZona>
              ))}
            </ZonaDrop>

            <ZonaDrop id="zonas-valores" titulo="Valores (agregaciones)" vacio="Arrastrá campos numéricos">
              {definicion.valores.map((v, i) => (
                <ItemZona key={v.campo} onQuitar={() => setDefinicion((d) => ({ ...d, valores: d.valores.filter((_, j) => j !== i) }))}
                  onSubir={() => setDefinicion((d) => ({ ...d, valores: mover(d.valores, i, -1) }))}
                  onBajar={() => setDefinicion((d) => ({ ...d, valores: mover(d.valores, i, 1) }))}>
                  <span className="text-xs font-medium">{v.campo}</span>
                  <select value={v.agregacion} onChange={(e) => setDefinicion((d) => ({
                    ...d, valores: d.valores.map((x, j) => j === i ? { ...x, agregacion: e.target.value as AgregacionReporte } : x)
                  }))} className="input !py-0.5 text-xs">
                    <option value="Suma">Suma</option><option value="Promedio">Promedio</option>
                    <option value="Maximo">Máximo</option><option value="Minimo">Mínimo</option>
                    <option value="Conteo">Conteo</option>
                  </select>
                  <input value={v.alias} onChange={(e) => setDefinicion((d) => ({
                    ...d, valores: d.valores.map((x, j) => j === i ? { ...x, alias: e.target.value } : x)
                  }))} className="input !py-0.5 !px-1 text-xs" placeholder="alias" />
                </ItemZona>
              ))}
            </ZonaDrop>

            <ZonaDrop id="zonas-columnas" titulo="Columnas (detalle)" vacio="Solo se usa si no hay agrupamiento">
              {definicion.columnas.map((c, i) => (
                <ItemZona key={c.campo} onQuitar={() => setDefinicion((d) => ({ ...d, columnas: d.columnas.filter((_, j) => j !== i) }))}
                  onSubir={() => setDefinicion((d) => ({ ...d, columnas: mover(d.columnas, i, -1) }))}
                  onBajar={() => setDefinicion((d) => ({ ...d, columnas: mover(d.columnas, i, 1) }))}>
                  <span className="text-xs font-medium">{c.campo}</span>
                  <input value={c.alias} onChange={(e) => setDefinicion((d) => ({
                    ...d, columnas: d.columnas.map((x, j) => j === i ? { ...x, alias: e.target.value } : x)
                  }))} className="input !py-0.5 !px-1 text-xs" />
                </ItemZona>
              ))}
            </ZonaDrop>

            <ZonaDrop id="zonas-filtros" titulo="Filtros" vacio="Arrastrá campos para filtrar">
              {definicion.filtros.map((f, i) => (
                <div key={f.campo + i} className="flex flex-wrap items-center gap-2 rounded border border-soft p-2">
                  <button onClick={() => setDefinicion((d) => ({ ...d, filtros: mover(d.filtros, i, -1) }))}
                    className="text-ink-muted hover:text-ink-primary" title="Subir"><ArrowUp size={12} /></button>
                  <button onClick={() => setDefinicion((d) => ({ ...d, filtros: mover(d.filtros, i, 1) }))}
                    className="text-ink-muted hover:text-ink-primary" title="Bajar"><ArrowDown size={12} /></button>
                  <span className="text-xs font-medium">{f.campo}</span>
                  <select value={f.operador} onChange={(e) => setDefinicion((d) => ({
                    ...d, filtros: d.filtros.map((x, j) => j === i ? { ...x, operador: e.target.value as OperadorFiltro } : x)
                  }))} className="input !py-0.5 text-xs">
                    {(OPERADORES_POR_TIPO[f.tipoDato] ?? ['Igual']).map((o) => (
                      <option key={o} value={o}>{ETIQUETA_OPERADOR[o]}</option>
                    ))}
                  </select>
                  <input value={f.etiqueta ?? ''} placeholder="etiqueta" onChange={(e) => setDefinicion((d) => ({
                    ...d, filtros: d.filtros.map((x, j) => j === i ? { ...x, etiqueta: e.target.value } : x)
                  }))} className="input !py-0.5 !px-1 w-28 text-xs" />
                  <label className="flex items-center gap-1 text-[10px]">
                    <input type="checkbox" checked={f.parametrizable} onChange={(e) => setDefinicion((d) => ({
                      ...d, filtros: d.filtros.map((x, j) => j === i ? { ...x, parametrizable: e.target.checked } : x)
                    }))} />
                    pregunta al ejecutar
                  </label>
                  {!f.parametrizable && f.operador !== 'Vacio' && f.operador !== 'NoVacio' && f.operador !== 'En' && (
                    <input value={f.valor ?? ''} placeholder="valor fijo" type={f.tipoDato === 'Numero' ? 'number' : 'text'}
                      onChange={(e) => setDefinicion((d) => ({
                        ...d, filtros: d.filtros.map((x, j) => j === i ? { ...x, valor: e.target.value } : x)
                      }))} className="input !py-0.5 !px-1 w-28 text-xs" />
                  )}
                  {!f.parametrizable && f.operador === 'Entre' && (
                    <input value={f.valor2 ?? ''} placeholder="valor hasta" type={f.tipoDato === 'Numero' ? 'number' : 'text'}
                      onChange={(e) => setDefinicion((d) => ({
                        ...d, filtros: d.filtros.map((x, j) => j === i ? { ...x, valor2: e.target.value } : x)
                      }))} className="input !py-0.5 !px-1 w-28 text-xs" />
                  )}
                  {!f.parametrizable && f.operador === 'En' && (
                    <input value={(f.valores ?? []).join(', ')} placeholder="valores, separados por coma" className="input !py-0.5 !px-1 w-40 text-xs"
                      onChange={(e) => setDefinicion((d) => ({
                        ...d, filtros: d.filtros.map((x, j) => j === i
                          ? { ...x, valores: e.target.value.split(',').map((s) => s.trim()).filter(Boolean) } : x)
                      }))} />
                  )}
                  <button onClick={() => setDefinicion((d) => ({ ...d, filtros: d.filtros.filter((_, j) => j !== i) }))}
                    className="btn-danger !p-1" title="Quitar filtro"><X size={12} /></button>
                </div>
              ))}
            </ZonaDrop>
          </div>
        </div>

        {/* Gráficos */}
        <div className="card space-y-2">
          <div className="flex items-center justify-between">
            <h3 className="flex items-center gap-2 text-sm font-semibold"><BarChart3 size={15} /> Gráficos</h3>
            <button
              onClick={() => setDefinicion((d) => ({
                ...d, graficos: [...d.graficos, {
                  titulo: 'Gráfico', tipo: 'Barras',
                  campoX: d.filas[0]?.alias ?? d.columnas[0]?.alias ?? '', camposY: d.valores[0] ? [d.valores[0].alias] : []
                }]
              }))}
              className="btn-secondary !px-2 !py-1 text-xs" disabled={definicion.valores.length === 0}
            ><Plus size={12} /> Agregar</button>
          </div>
          {definicion.graficos.map((g, i) => (
            <div key={i} className="flex flex-wrap items-center gap-2 rounded border border-soft p-2">
              <input value={g.titulo} onChange={(e) => setDefinicion((d) => ({
                ...d, graficos: d.graficos.map((x, j) => j === i ? { ...x, titulo: e.target.value } : x)
              }))} className="input !py-0.5 !px-1 w-36 text-xs" />
              <select value={g.tipo} onChange={(e) => setDefinicion((d) => ({
                ...d, graficos: d.graficos.map((x, j) => j === i ? { ...x, tipo: e.target.value as TipoGrafico } : x)
              }))} className="input !py-0.5 text-xs">
                <option value="Barras">Barras</option><option value="Lineas">Líneas</option><option value="Torta">Torta</option>
              </select>
              <select value={g.campoX} onChange={(e) => setDefinicion((d) => ({
                ...d, graficos: d.graficos.map((x, j) => j === i ? { ...x, campoX: e.target.value } : x)
              }))} className="input !py-0.5 text-xs">
                <option value="">eje X…</option>
                {[...definicion.filas.map((f) => f.alias), ...definicion.columnas.map((c) => c.alias)].map((a) => (
                  <option key={a} value={a}>{a}</option>
                ))}
              </select>
              <div className="flex flex-wrap items-center gap-2">
                {definicion.valores.map((v) => (
                  <label key={v.alias} className="flex items-center gap-1 text-[10px]">
                    <input
                      type="checkbox"
                      checked={g.camposY.includes(v.alias)}
                      onChange={(e) => setDefinicion((d) => ({
                        ...d,
                        graficos: d.graficos.map((x, j) => {
                          if (j !== i) return x;
                          const camposY = e.target.checked
                            ? [...x.camposY, v.alias]
                            : x.camposY.filter((y) => y !== v.alias);
                          return { ...x, camposY };
                        })
                      }))}
                    />
                    {v.alias}
                  </label>
                ))}
              </div>
              <button onClick={() => setDefinicion((d) => ({ ...d, graficos: d.graficos.filter((_, j) => j !== i) }))}
                className="btn-danger !p-1"><X size={12} /></button>
            </div>
          ))}
        </div>

        {/* Subreportes */}
        <div className="card space-y-2">
          <div className="flex items-center justify-between">
            <h3 className="flex items-center gap-2 text-sm font-semibold"><Table2 size={15} /> Subreportes</h3>
            <button
              onClick={() => setDefinicion((d) => ({
                ...d, subreportes: [...d.subreportes, { reporteId: subreportesDisponibles[0]?.id ?? '', nombre: null, camposRelacion: [] }]
              }))}
              className="btn-secondary !px-2 !py-1 text-xs" disabled={subreportesDisponibles.length === 0 || !id}
            ><Plus size={12} /> Agregar</button>
          </div>
          {subreportesDisponibles.length === 0 && !id && (
            <p className="text-xs text-ink-secondary">Guardá este reporte primero para poder vincular subreportes.</p>
          )}
          {definicion.subreportes.map((s, i) => (
            <div key={i} className="space-y-2 rounded border border-soft p-2">
              <div className="flex items-center gap-2">
                <select
                  value={s.reporteId}
                  onChange={(e) => setDefinicion((d) => ({
                    ...d, subreportes: d.subreportes.map((x, j) => j === i
                      ? { ...x, reporteId: e.target.value, nombre: subreportesDisponibles.find((r) => r.id === e.target.value)?.nombre ?? null } : x)
                  }))}
                  className="input !py-0.5 text-xs"
                >
                  {subreportesDisponibles.map((r) => <option key={r.id} value={r.id}>{r.nombre}</option>)}
                </select>
                <button onClick={() => setDefinicion((d) => ({ ...d, subreportes: d.subreportes.filter((_, j) => j !== i) }))}
                  className="btn-danger !p-1"><X size={12} /></button>
              </div>
              <div className="space-y-1 pl-3">
                <p className="text-[10px] uppercase tracking-wide text-ink-muted">Campos de relación (padre → hijo)</p>
                {s.camposRelacion.map((rel, ri) => (
                  <div key={ri} className="flex items-center gap-1">
                    <select value={rel.campoPadre} onChange={(e) => setDefinicion((d) => ({
                      ...d, subreportes: d.subreportes.map((x, j) => j === i
                        ? { ...x, camposRelacion: x.camposRelacion.map((r2, k) => k === ri ? { ...r2, campoPadre: e.target.value } : r2) } : x)
                    }))} className="input !py-0.5 text-xs">
                      <option value="">campo padre…</option>
                      {[...definicion.filas.map((f) => ({ a: f.alias, c: f.campo })), ...definicion.columnas.map((c) => ({ a: c.alias, c: c.campo }))].map((o) => (
                        <option key={o.c} value={o.c}>{o.a}</option>
                      ))}
                    </select>
                    <span className="text-xs">→</span>
                    <select value={rel.campoHijo} onChange={(e) => setDefinicion((d) => ({
                      ...d, subreportes: d.subreportes.map((x, j) => j === i
                        ? { ...x, camposRelacion: x.camposRelacion.map((r2, k) => k === ri ? { ...r2, campoHijo: e.target.value } : r2) } : x)
                    }))} className="input !py-0.5 text-xs">
                      <option value="">campo hijo…</option>
                      {campos.map((c) => <option key={c.nombre} value={c.nombre}>{c.nombre}</option>)}
                    </select>
                    <button
                      onClick={() => setDefinicion((d) => ({
                        ...d, subreportes: d.subreportes.map((x, j) => j === i
                          ? { ...x, camposRelacion: x.camposRelacion.filter((_, k) => k !== ri) } : x)
                      }))}
                      className="text-ink-muted hover:text-danger"><X size={11} /></button>
                  </div>
                ))}
                <button
                  onClick={() => setDefinicion((d) => ({
                    ...d, subreportes: d.subreportes.map((x, j) => j === i
                      ? { ...x, camposRelacion: [...x.camposRelacion, { campoPadre: '', campoHijo: '' }] } : x)
                  }))}
                  className="text-xs text-blue-600 hover:underline"
                >+ relación</button>
              </div>
            </div>
          ))}
        </div>

        {error && <p className="text-sm text-danger">{error}</p>}

        {/* Acciones al pie */}
        <div className="flex flex-wrap items-center justify-end gap-2 border-t border-soft pt-3">
          {!id ? (
            <button onClick={() => guardar(true)} disabled={guardando} className="btn-primary flex items-center gap-2">
              {guardando ? <Loader2 size={14} className="animate-spin" /> : <Save size={14} />}
              Guardar y cargar campos
            </button>
          ) : (
            <>
              <span className="badge tint-success text-success mr-auto">campos cargados</span>
              <button onClick={() => guardar(false)} disabled={guardando} className="btn-primary flex items-center gap-2">
                {guardando ? <Loader2 size={14} className="animate-spin" /> : <Save size={14} />}
                Guardar cambios
              </button>
            </>
          )}
          <button onClick={onCerrar} className="btn-secondary">Cerrar</button>
        </div>
      </div>

      {asistente && (
        <AsistenteVisual
          conexion={conexion}
          onGenerar={(sql) => { setQuerySql(sql); setAsistente(false); setError(null); }}
          onCerrar={() => setAsistente(false)}
        />
      )}
    </DndContext>
  );
}

function CampoArrastrable({ nombre, tipo }: { nombre: string; tipo: TipoDatoReporte }) {
  const { attributes, listeners, setNodeRef, isDragging } = useDraggable({
    id: `campo:${nombre}:${tipo}`, data: { campo: nombre, tipo }
  });
  return (
    <div
      ref={setNodeRef}
      {...listeners}
      {...attributes}
      className={`flex cursor-grab items-center justify-between rounded border border-soft bg-base px-2 py-1.5 text-xs active:cursor-grabbing ${isDragging ? 'opacity-40' : ''}`}
    >
      <span className="font-medium">{nombre}</span>
      <span className="badge bg-black/5 text-[10px] text-ink-muted">{tipo}</span>
    </div>
  );
}

function ZonaDrop({ id, titulo, vacio, children }: {
  id: string; titulo: string; vacio: string; children: React.ReactNode;
}) {
  const { setNodeRef, isOver } = useDroppable({ id });
  return (
    <div
      ref={setNodeRef}
      className={`card min-h-[52px] space-y-2 ${isOver ? 'ring-2 ring-blue-500' : ''}`}
    >
      <h3 className="text-xs font-semibold uppercase tracking-wide text-ink-muted">{titulo}</h3>
      {vacio && <p className="text-[10px] text-ink-muted">{vacio}</p>}
      {children}
    </div>
  );
}

function ItemZona({ children, onQuitar, onSubir, onBajar }: {
  children: React.ReactNode; onQuitar: () => void; onSubir?: () => void; onBajar?: () => void;
}) {
  return (
    <div className="flex items-center gap-2 rounded border border-soft p-1.5">
      {onSubir && <button onClick={onSubir} className="text-ink-muted hover:text-ink-primary"><ArrowUp size={12} /></button>}
      {onBajar && <button onClick={onBajar} className="text-ink-muted"><ArrowDown size={12} /></button>}
      {children}
      <button onClick={onQuitar} className="ml-auto text-ink-muted hover:text-danger"><X size={12} /></button>
    </div>
  );
}
