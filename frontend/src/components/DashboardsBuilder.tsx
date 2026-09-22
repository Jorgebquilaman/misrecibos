import { useEffect, useState } from 'react';
import { BarChart3, Gauge, Loader2, Plus, Save, Table2, Trash2 } from 'lucide-react';
import { dashboardsApi, reportesApi } from '../api';
import type {
  AgregacionReporte, ColumnaMetadata, DashboardCompletoDto, DashboardDefinicionDto,
  ResultadoDashboardDto
} from '../types';

const defVacia = (): DashboardDefinicionDto => ({
  filtros: [], kpis: [], graficos: [], tablas: [], limite: 5000
});

export default function DashboardsBuilder({ dashboard, onGuardado, onCerrar }: {
  dashboard: DashboardCompletoDto | null;
  onGuardado: (id: string) => void;
  onCerrar: () => void;
}) {
  const [nombre, setNombre] = useState(dashboard?.nombre ?? '');
  const [descripcion, setDescripcion] = useState(dashboard?.descripcion ?? '');
  const [querySql, setQuerySql] = useState(dashboard?.querySql ?? '');
  const [definicion, setDefinicion] = useState<DashboardDefinicionDto>(dashboard?.definicion ?? defVacia());
  const [id, setId] = useState<string | null>(dashboard?.id ?? null);
  const [conexiones, setConexiones] = useState<string[]>([]);
  const [conexion, setConexion] = useState(dashboard?.conexion ?? 'PortalIUPA');
  const [campos, setCampos] = useState<ColumnaMetadata[]>([]);
  const [guardando, setGuardando] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [preview, setPreview] = useState<ResultadoDashboardDto | null>(null);
  const [cargandoPreview, setCargandoPreview] = useState(false);

  useEffect(() => {
    reportesApi.conexiones().then(setConexiones).catch(() => {});
    if (dashboard) {
      void cargarCampos(dashboard.querySql, dashboard.conexion);
    }
  }, [dashboard]);

  const cargarCampos = async (sql: string, conex?: string) => {
    try {
      setCampos(await reportesApi.metadataSql(sql, conex ?? conexion));
    } catch { /* campos quedan vacíos */ }
  };

  const actualizarCampos = async () => {
    if (!querySql.trim()) return;
    setError(null);
    try {
      setCampos(await reportesApi.metadataSql(querySql, conexion));
    } catch (e: unknown) {
      const err = e as { response?: { data?: { error?: string } } };
      setError(err.response?.data?.error ?? 'Error al leer los campos de la consulta');
    }
  };

  const tipoDeCampo = (campo: string): 'Texto' | 'Numero' | 'Fecha' | 'Booleano' =>
    campos.find((c) => c.nombre === campo)?.tipo ?? 'Texto';

  const guardar = async (crear: boolean) => {
    if (!nombre.trim() || !querySql.trim()) {
      setError('Completá nombre y consulta SQL.');
      return;
    }
    setGuardando(true);
    setError(null);
    try {
      const res = crear
        ? await dashboardsApi.crear(nombre, descripcion || null, querySql, conexion, definicion)
        : await dashboardsApi.actualizar(id!, nombre, descripcion || null, querySql, conexion, definicion);
      setId(res.id);
      await cargarCampos(querySql, conexion);
      onGuardado(res.id);
    } catch (e: unknown) {
      const err = e as { response?: { data?: { error?: string } } };
      setError(err.response?.data?.error ?? 'Error al guardar el dashboard');
    } finally {
      setGuardando(false);
    }
  };

  const vistaPrevia = async () => {
    setCargandoPreview(true);
    setError(null);
    try {
      setPreview(await dashboardsApi.vistaPrevia(querySql, conexion, definicion, {}));
    } catch (e: unknown) {
      const err = e as { response?: { data?: { error?: string } } };
      setError(err.response?.data?.error ?? 'Error en la vista previa');
    } finally {
      setCargandoPreview(false);
    }
  };

  const nombresCampo = campos.map((c) => c.nombre);

  return (
    <div className="space-y-4">
      {/* Identidad */}
      <div className="card grid grid-cols-1 gap-3 sm:grid-cols-2">
        <label className="text-xs">
          <span className="mb-1 block font-medium text-ink-secondary">Nombre</span>
          <input value={nombre} onChange={(e) => setNombre(e.target.value)} className="input w-full" placeholder="Ej: Dashboard Liquidaciones" />
        </label>
        <label className="text-xs">
          <span className="mb-1 block font-medium text-ink-secondary">Descripción</span>
          <input value={descripcion} onChange={(e) => setDescripcion(e.target.value)} className="input w-full" />
        </label>
        <label className="text-xs">
          <span className="mb-1 block font-medium text-ink-secondary">Conexión</span>
          <select value={conexion} onChange={(e) => setConexion(e.target.value)} className="input w-full">
            {conexiones.map((c) => <option key={c} value={c}>{c}</option>)}
          </select>
        </label>
        <label className="text-xs sm:col-span-2">
          <span className="mb-1 block font-medium text-ink-secondary">Consulta SQL base (solo SELECT) — comparten todos los controles y filtros</span>
          <textarea value={querySql} onChange={(e) => setQuerySql(e.target.value)} rows={4} className="input w-full font-mono text-xs" />
        </label>
      </div>

      {error && <p className="text-sm text-danger">{error}</p>}

      {/* Campos disponibles */}
      <div className="card">
        <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
          <h3 className="text-sm font-semibold">Campos disponibles ({campos.length})</h3>
          <div className="flex items-center gap-1">
            <button
              onClick={vistaPrevia}
              disabled={cargandoPreview || !querySql.trim()}
              className="btn-secondary !px-2 !py-0.5 text-[11px]"
              title="Ver los KPIs/gráficos actuales sobre la consulta"
            >
              {cargandoPreview ? <Loader2 size={12} className="animate-spin" /> : null} Probar
            </button>
            <button
              onClick={actualizarCampos}
              disabled={!querySql.trim()}
              className="btn-secondary !px-2 !py-0.5 text-[11px]"
              title="Lee los campos de la consulta actual (sin guardar)"
            >Actualizar campos</button>
          </div>
        </div>
        {campos.length === 0 ? (
          <p className="text-xs text-ink-secondary">
            Escribí la consulta y presioná "Actualizar campos" (o guardá el dashboard) para ver las columnas que podés usar en los componentes.
          </p>
        ) : (
          <div className="flex max-h-32 flex-wrap gap-1 overflow-auto">
            {campos.map((c) => (
              <span key={c.nombre} className="badge bg-black/5 text-[10px] text-ink-secondary">
                {c.nombre} <span className="text-ink-muted">({c.tipo})</span>
              </span>
            ))}
          </div>
        )}
      </div>

      {/* KPIs / KPOs */}
      <div className="card space-y-2">
        <div className="flex items-center justify-between">
          <h3 className="flex items-center gap-2 text-sm font-semibold"><Gauge size={15} /> KPIs y KPOs</h3>
          <button
            onClick={() => setDefinicion((d) => ({
              ...d, kpis: [...d.kpis, {
                titulo: 'KPI', campo: nombresCampo[0] ?? '', agregacion: 'Suma' as AgregacionReporte,
                formato: 'numero', esKpo: false, objetivo: null, color: null
              }]
            }))}
            className="btn-secondary !px-2 !py-1 text-xs" disabled={nombresCampo.length === 0}
          ><Plus size={12} /> Agregar</button>
        </div>
        {definicion.kpis.map((k, i) => (
          <div key={i} className="flex flex-wrap items-center gap-2 rounded border border-soft p-2 text-xs">
            <input value={k.titulo} onChange={(e) => setDefinicion((d) => ({
              ...d, kpis: d.kpis.map((x, j) => j === i ? { ...x, titulo: e.target.value } : x)
            }))} className="input !py-0.5 !px-1 w-40" placeholder="título" />
            <select value={k.campo} onChange={(e) => setDefinicion((d) => ({
              ...d, kpis: d.kpis.map((x, j) => j === i ? { ...x, campo: e.target.value } : x)
            }))} className="input !py-0.5 text-xs">
              {nombresCampo.map((c) => <option key={c} value={c}>{c}</option>)}
            </select>
            <select value={k.agregacion} onChange={(e) => setDefinicion((d) => ({
              ...d, kpis: d.kpis.map((x, j) => j === i ? { ...x, agregacion: e.target.value as AgregacionReporte } : x)
            }))} className="input !py-0.5 text-xs">
              <option value="Suma">Suma</option><option value="Promedio">Promedio</option>
              <option value="Maximo">Máximo</option><option value="Minimo">Mínimo</option>
              <option value="Conteo">Conteo</option>
            </select>
            <select value={k.formato} onChange={(e) => setDefinicion((d) => ({
              ...d, kpis: d.kpis.map((x, j) => j === i ? { ...x, formato: e.target.value as 'numero' | 'moneda' | 'porcentaje' } : x)
            }))} className="input !py-0.5 text-xs">
              <option value="numero">número</option><option value="moneda">moneda</option><option value="porcentaje">%</option>
            </select>
            <label className="flex items-center gap-1">
              <input type="checkbox" checked={k.esKpo} onChange={(e) => setDefinicion((d) => ({
                ...d, kpis: d.kpis.map((x, j) => j === i ? { ...x, esKpo: e.target.checked } : x)
              }))} />
              KPO
            </label>
            {k.esKpo && (
              <input type="number" value={k.objetivo ?? ''} placeholder="objetivo"
                onChange={(e) => setDefinicion((d) => ({
                  ...d, kpis: d.kpis.map((x, j) => j === i ? { ...x, objetivo: e.target.value === '' ? null : Number(e.target.value) } : x)
                }))} className="input !py-0.5 !px-1 w-28" />
            )}
            <input type="color" value={k.color ?? '#2563eb'} title="color del valor"
              onChange={(e) => setDefinicion((d) => ({
                ...d, kpis: d.kpis.map((x, j) => j === i ? { ...x, color: e.target.value } : x)
              }))} className="h-6 w-8 cursor-pointer" />
            <button onClick={() => setDefinicion((d) => ({ ...d, kpis: d.kpis.filter((_, j) => j !== i) }))}
              className="text-ink-muted hover:text-danger"><Trash2 size={13} /></button>
          </div>
        ))}
      </div>

      {/* Gráficos */}
      <div className="card space-y-2">
        <div className="flex items-center justify-between">
          <h3 className="flex items-center gap-2 text-sm font-semibold"><BarChart3 size={15} /> Gráficos</h3>
          <button
            onClick={() => setDefinicion((d) => ({
              ...d, graficos: [...d.graficos, {
                titulo: 'Gráfico', tipo: 'Barras', campoX: nombresCampo[0] ?? '',
                campoY: nombresCampo[1] ?? nombresCampo[0] ?? '', agregacion: 'Suma' as AgregacionReporte,
                ordenarValorDesc: true, ancho: 'mitad'
              }]
            }))}
            className="btn-secondary !px-2 !py-1 text-xs" disabled={nombresCampo.length === 0}
          ><Plus size={12} /> Agregar</button>
        </div>
        {definicion.graficos.map((g, i) => (
          <div key={i} className="flex flex-wrap items-center gap-2 rounded border border-soft p-2 text-xs">
            <input value={g.titulo} onChange={(e) => setDefinicion((d) => ({
              ...d, graficos: d.graficos.map((x, j) => j === i ? { ...x, titulo: e.target.value } : x)
            }))} className="input !py-0.5 !px-1 w-36" />
            <select value={g.tipo} onChange={(e) => setDefinicion((d) => ({
              ...d, graficos: d.graficos.map((x, j) => j === i ? { ...x, tipo: e.target.value as 'Barras' | 'Lineas' | 'Torta' | 'Area' | 'Dona' } : x)
            }))} className="input !py-0.5 text-xs">
              <option value="Barras">Barras</option><option value="Lineas">Líneas</option>
              <option value="Torta">Torta</option><option value="Area">Área</option><option value="Dona">Dona</option>
            </select>
            <span className="text-ink-muted">eje X:</span>
            <select value={g.campoX} onChange={(e) => setDefinicion((d) => ({
              ...d, graficos: d.graficos.map((x, j) => j === i ? { ...x, campoX: e.target.value } : x)
            }))} className="input !py-0.5 text-xs">
              {nombresCampo.map((c) => <option key={c} value={c}>{c}</option>)}
            </select>
            <span className="text-ink-muted">valor:</span>
            <select value={g.campoY} onChange={(e) => setDefinicion((d) => ({
              ...d, graficos: d.graficos.map((x, j) => j === i ? { ...x, campoY: e.target.value } : x)
            }))} className="input !py-0.5 text-xs">
              {nombresCampo.map((c) => <option key={c} value={c}>{c}</option>)}
            </select>
            <select value={g.agregacion} onChange={(e) => setDefinicion((d) => ({
              ...d, graficos: d.graficos.map((x, j) => j === i ? { ...x, agregacion: e.target.value as AgregacionReporte } : x)
            }))} className="input !py-0.5 text-xs">
              <option value="Suma">Suma</option><option value="Promedio">Promedio</option>
              <option value="Maximo">Máx</option><option value="Minimo">Mín</option>
              <option value="Conteo">Conteo</option>
            </select>
            <label className="flex items-center gap-1">
              <input type="checkbox" checked={g.ordenarValorDesc} onChange={(e) => setDefinicion((d) => ({
                ...d, graficos: d.graficos.map((x, j) => j === i ? { ...x, ordenarValorDesc: e.target.checked } : x)
              }))} /> ordenar desc
            </label>
            <select value={g.ancho} onChange={(e) => setDefinicion((d) => ({
              ...d, graficos: d.graficos.map((x, j) => j === i ? { ...x, ancho: e.target.value as 'completo' | 'mitad' } : x)
            }))} className="input !py-0.5 text-xs">
              <option value="mitad">mitad</option><option value="completo">ancho completo</option>
            </select>
            <button onClick={() => setDefinicion((d) => ({ ...d, graficos: d.graficos.filter((_, j) => j !== i) }))}
              className="text-ink-muted hover:text-danger"><Trash2 size={13} /></button>
          </div>
        ))}
      </div>

      {/* Filtros globales */}
      <div className="card space-y-2">
        <div className="flex items-center justify-between">
          <h3 className="flex items-center gap-2 text-sm font-semibold"><Table2 size={15} /> Filtros globales</h3>
          <button
            onClick={() => setDefinicion((d) => ({
              ...d, filtros: [...d.filtros, {
                campo: nombresCampo[0] ?? '',
                tipoDato: campos.find((c) => c.nombre === nombresCampo[0])?.tipo ?? ('Texto' as const),
                operador: 'Igual' as const,
                etiqueta: null, valor: null, valor2: null, valores: null
              }]
            }))}
            className="btn-secondary !px-2 !py-1 text-xs" disabled={nombresCampo.length === 0}
          ><Plus size={12} /> Agregar</button>
        </div>
        <p className="text-[10px] text-ink-muted">Se aplican a todos los KPIs, gráficos y tablas del dashboard.</p>
        {definicion.filtros.map((f, i) => (
          <div key={i} className="flex flex-wrap items-center gap-2 rounded border border-soft p-2 text-xs">
            <select value={f.campo} onChange={(e) => setDefinicion((d) => ({
              ...d, filtros: d.filtros.map((x, j) => j === i ? { ...x, campo: e.target.value, tipoDato: tipoDeCampo(e.target.value) } : x)
            }))} className="input !py-0.5 text-xs">
              {nombresCampo.map((c) => <option key={c} value={c}>{c}</option>)}
            </select>
            <select value={f.operador} onChange={(e) => setDefinicion((d) => ({
              ...d, filtros: d.filtros.map((x, j) => j === i ? { ...x, operador: e.target.value as 'Igual' | 'Entre' | 'MayorIgual' | 'MenorIgual' | 'Contiene' } : x)
            }))} className="input !py-0.5 text-xs">
              <option value="Igual">igual a</option><option value="Contiene">contiene</option>
              <option value="MayorIgual">mayor o igual</option><option value="MenorIgual">menor o igual</option>
              <option value="Entre">entre</option>
            </select>
            <input value={f.etiqueta ?? ''} placeholder="etiqueta visible" onChange={(e) => setDefinicion((d) => ({
              ...d, filtros: d.filtros.map((x, j) => j === i ? { ...x, etiqueta: e.target.value } : x)
            }))} className="input !py-0.5 !px-1 w-32" />
            <button onClick={() => setDefinicion((d) => ({ ...d, filtros: d.filtros.filter((_, j) => j !== i) }))}
              className="text-ink-muted hover:text-danger"><Trash2 size={13} /></button>
          </div>
        ))}
      </div>

      {/* Tablas */}
      <div className="card space-y-2">
        <div className="flex items-center justify-between">
          <h3 className="flex items-center gap-2 text-sm font-semibold"><Table2 size={15} /> Tablas de detalle</h3>
          <button
            onClick={() => setDefinicion((d) => ({
              ...d, tablas: [...d.tablas, { titulo: 'Detalle', limite: 50 }]
            }))}
            className="btn-secondary !px-2 !py-1 text-xs"
          ><Plus size={12} /> Agregar</button>
        </div>
        {definicion.tablas.map((t, i) => (
          <div key={i} className="flex flex-wrap items-center gap-2 rounded border border-soft p-2 text-xs">
            <input value={t.titulo} onChange={(e) => setDefinicion((d) => ({
              ...d, tablas: d.tablas.map((x, j) => j === i ? { ...x, titulo: e.target.value } : x)
            }))} className="input !py-0.5 !px-1 w-40" />
            <span>límite de filas:</span>
            <input type="number" value={t.limite} min={10} max={200}
              onChange={(e) => setDefinicion((d) => ({
                ...d, tablas: d.tablas.map((x, j) => j === i ? { ...x, limite: Number(e.target.value) } : x)
              }))} className="input !py-0.5 !px-1 w-20" />
            <button onClick={() => setDefinicion((d) => ({ ...d, tablas: d.tablas.filter((_, j) => j !== i) }))}
              className="text-ink-muted hover:text-danger"><Trash2 size={13} /></button>
          </div>
        ))}
      </div>

      {/* Acciones al pie */}
      <div className="flex flex-wrap items-center justify-end gap-2 border-t border-soft pt-3">
        <button onClick={vistaPrevia} disabled={cargandoPreview} className="btn-secondary flex items-center gap-2 text-sm">
          {cargandoPreview ? <Loader2 size={14} className="animate-spin" /> : null} Vista previa
        </button>
        <button onClick={() => guardar(!id)} disabled={guardando} className="btn-primary flex items-center gap-2">
          {guardando ? <Loader2 size={14} className="animate-spin" /> : <Save size={14} />}
          {id ? 'Guardar cambios' : 'Guardar'}
        </button>
        <button onClick={onCerrar} className="btn-secondary">Cerrar</button>
      </div>

      {/* Vista previa en vivo */}
      {preview && (
        <div className="space-y-3 border-t border-soft pt-4">
          <h3 className="text-sm font-semibold">Vista previa (sin filtros)</h3>
          {preview.kpis.length > 0 && (
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
              {preview.kpis.map((k, i) => (
                <div key={i} className="card p-3">
                  <p className="text-[10px] uppercase tracking-wide text-ink-muted">{k.titulo}</p>
                  <p className="text-xl font-bold tabular-nums" style={{ color: k.color ?? undefined }}>
                    {k.valor.toLocaleString('es-AR', { maximumFractionDigits: 2 })}
                  </p>
                </div>
              ))}
            </div>
          )}
          {preview.graficos.map((g, i) => (
            <div key={i} className="card">
              <h4 className="mb-1 text-xs font-semibold">{g.titulo}</h4>
              <p className="text-xs text-ink-secondary">{g.datos.filas.length} grupos · {g.tipo}</p>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
