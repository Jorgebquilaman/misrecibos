import { useEffect, useMemo, useState } from 'react';
import {
  Area, AreaChart, Bar, BarChart, CartesianGrid, Cell, Legend, Line, LineChart,
  Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis
} from 'recharts';
import { Loader2 } from 'lucide-react';
import { dashboardsApi } from '../api';
import type {
  DashboardCompletoDto, ResultadoDashboardDto
} from '../types';

const COLORES = ['#2563eb', '#16a34a', '#ea580c', '#9333ea', '#0891b2', '#dc2626', '#ca8a04', '#0d9488'];

function formatearKpi(valor: number, formato: string): string {
  if (formato === 'moneda') return valor.toLocaleString('es-AR', { style: 'currency', currency: 'ARS', maximumFractionDigits: 2 });
  if (formato === 'porcentaje') return `${valor.toLocaleString('es-AR', { maximumFractionDigits: 1 })}%`;
  return valor.toLocaleString('es-AR', { maximumFractionDigits: 2 });
}

/** Tarjeta KPI o KPO (con objetivo: barra de progreso + % cumplimiento). */
function TarjetaKpi({ kpi }: { kpi: ResultadoDashboardDto['kpis'][number] }) {
  const porcentaje = kpi.esKpo && kpi.objetivo && kpi.objetivo > 0
    ? Math.min(100, (kpi.valor / kpi.objetivo) * 100)
    : null;
  const colorBarra = porcentaje === null ? '#2563eb'
    : porcentaje >= 100 ? '#16a34a'
    : porcentaje >= 70 ? '#ca8a04'
    : '#dc2626';

  return (
    <div className="card min-w-0 p-4">
      <p className="truncate text-xs font-medium uppercase tracking-wide text-ink-muted">{kpi.titulo}</p>
      <p className="mt-1 truncate text-2xl font-bold tabular-nums" style={{ color: kpi.color ?? undefined }}>
        {formatearKpi(kpi.valor, kpi.formato)}
      </p>
      {kpi.esKpo && kpi.objetivo !== null && kpi.objetivo > 0 && (
        <div className="mt-2">
          <div className="h-2 w-full overflow-hidden rounded-full bg-black/10">
            <div className="h-full rounded-full transition-all" style={{ width: `${porcentaje}%`, backgroundColor: colorBarra }} />
          </div>
          <p className="mt-1 text-[10px] text-ink-secondary">
            objetivo: {formatearKpi(kpi.objetivo, kpi.formato)} · {porcentaje!.toLocaleString('es-AR', { maximumFractionDigits: 0 })}% cumplido
          </p>
        </div>
      )}
    </div>
  );
}

export default function DashboardViewer({ dashboard, onCerrar }: {
  dashboard: DashboardCompletoDto;
  onCerrar?: () => void;
}) {
  const def = dashboard.definicion;
  const [valores, setValores] = useState<Record<string, string>>({});
  const [chips, setChips] = useState<{ campo: string; valor: string }[]>([]);
  const [ejecutando, setEjecutando] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [resultado, setResultado] = useState<ResultadoDashboardDto | null>(null);
  const [ejecutado, setEjecutado] = useState(false);

  const ejecutar = async (valoresExtra?: Record<string, string | string[]>) => {
    setEjecutado(true);
    setEjecutando(true);
    setError(null);
    try {
      const payload: Record<string, unknown> = {};
      for (const f of def.filtros) {
        const v = valores[f.campo];
        if (v === undefined || v === '') continue;
        payload[f.campo] = f.tipoDato === 'Numero' ? Number(v) : v;
      }
      for (const chip of chips) payload[chip.campo] = chip.valor;
      if (valoresExtra) Object.assign(payload, valoresExtra);
      setResultado(await dashboardsApi.ejecutar(dashboard.id, payload));
    } catch (e: unknown) {
      const err = e as { response?: { data?: { error?: string } } };
      setError(err.response?.data?.error ?? 'Error al ejecutar el dashboard');
    } finally {
      setEjecutando(false);
    }
  };

  // Sin filtros: carga todo automáticamente. Con filtros: solo cuando el usuario aplica.
  useEffect(() => {
    if (!def.filtros.length) void ejecutar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [dashboard.id]);

  // Los filtros del formulario se aplican con el botón; los chips (clic en gráfico) al instante.
  useEffect(() => {
    if (chips.length > 0) void ejecutar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [chips]);


  const graficoDatos = useMemo(() => {
    if (!resultado) return [];
    return resultado.graficos.map((g) => ({
      grafico: g,
      data: g.datos.filas.map((f) => ({
        campoX: String(f[g.datos.columnas[0]] ?? ''),
        valor: typeof f['valor'] === 'number' ? (f['valor'] as number) : 0
      }))
    }));
  }, [resultado]);

  const hayFiltros = def.filtros.length > 0;
  const sinFiltrosAplicados = !ejecutado;

  return (
    <div className="space-y-4">
      {onCerrar && (
        <div className="flex items-center justify-between">
          <button onClick={onCerrar} className="btn-secondary text-sm">← Volver</button>
          {ejecutando && <Loader2 size={16} className="animate-spin" />}
        </div>
      )}

      {/* Filtros globales: afectan a todos los controles */}
      {hayFiltros && (
        <div className="card space-y-3">
          <h3 className="text-sm font-semibold">Filtros (se aplican a todos los controles)</h3>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            {def.filtros.map((f, i) => (
              <label key={f.campo + i} className="text-xs">
                <span className="mb-1 block font-medium text-ink-secondary">
                  {f.etiqueta ?? f.campo}
                </span>
                <input
                  type={f.tipoDato === 'Numero' ? 'number' : f.tipoDato === 'Fecha' ? 'datetime-local' : 'text'}
                  value={valores[f.campo] ?? ''}
                  onChange={(e) => setValores((v) => ({ ...v, [f.campo]: e.target.value }))}
                  className="input w-full"
                />
              </label>
            ))}
          </div>
          <button onClick={() => ejecutar()} disabled={ejecutando} className="btn-primary flex items-center gap-2 text-sm">
            {ejecutando ? <Loader2 size={14} className="animate-spin" /> : null} Aplicar filtros
          </button>
        </div>
      )}

      {/* Chips de cross-filtering (clic en los gráficos) */}
      {chips.length > 0 && (
        <div className="flex flex-wrap items-center gap-1 text-xs">
          <span className="text-ink-muted">filtrando por:</span>
          {chips.map((c, i) => (
            <span key={i} className="badge tint-success text-success">
              {c.campo} = {c.valor}
              <button onClick={() => setChips(chips.filter((_, j) => j !== i))} className="ml-1">✕</button>
            </span>
          ))}
          <button onClick={() => setChips([])} className="text-ink-muted hover:text-danger">limpiar todo</button>
        </div>
      )}

      {error && <p className="text-sm text-danger">{error}</p>}

      {sinFiltrosAplicados && hayFiltros && (
        <div className="card flex flex-col items-center gap-2 py-8 text-center">
          <p className="text-sm text-ink-secondary">
            Completá los filtros y presioná <strong>"Aplicar filtros"</strong> para cargar los datos
            (sin filtro se muestra todo).
          </p>
        </div>
      )}

      {resultado && (
        <>
          {resultado.kpis.length > 0 && (
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
              {resultado.kpis.map((kpi, i) => <TarjetaKpi key={i} kpi={kpi} />)}
            </div>
          )}

          {resultado.graficos.length > 0 && (
            <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
              {graficoDatos.map(({ grafico, data }, i) => (
                <div key={i} className={`card ${grafico.anchoCompleto ? 'lg:col-span-2' : ''}`}>
                  <h3 className="mb-2 text-sm font-semibold">{grafico.titulo}</h3>
                  <div style={{ height: 280 }}>
                    <ResponsiveContainer width="100%" height="100%">
                      {grafico.tipo === 'Torta' || grafico.tipo === 'Dona' ? (
                        <PieChart>
                          <Pie
                            data={data}
                            dataKey="valor"
                            nameKey="campoX"
                            innerRadius={grafico.tipo === 'Dona' ? 60 : 0}
                            outerRadius={95}
                            label
                            onClick={((e: { payload?: { campoX?: string } }) => {
                              const valor = e?.payload?.campoX;
                              if (valor) {
                                const gx = dashboard.definicion.graficos[i]?.campoX;
                                if (gx) setChips((prev) => [...prev.filter((c) => c.campo !== gx), { campo: gx, valor: String(valor) }]);
                              }
                            }) as never}
                          >
                            {data.map((_, j) => <Cell key={j} fill={COLORES[j % COLORES.length]} />)}
                          </Pie>
                          <Tooltip />
                          <Legend />
                        </PieChart>
                      ) : grafico.tipo === 'Lineas' ? (
                          <LineChart data={data} onClick={((e: { activeLabel?: string }) => {
                            if (e?.activeLabel) setChips((prev) => [...prev.filter((c) => c.campo !== grafico.datos.columnas[0]), { campo: grafico.datos.columnas[0], valor: String(e.activeLabel) }]);
                          }) as never}>
                          <CartesianGrid strokeDasharray="3 3" opacity={0.3} />
                          <XAxis dataKey="campoX" fontSize={11} />
                          <YAxis fontSize={11} tickFormatter={(v: number) => v.toLocaleString('es-AR')} />
                          <Tooltip formatter={((v: unknown) => typeof v === 'number' ? v.toLocaleString('es-AR') : String(v)) as never} />
                          <Line type="monotone" dataKey="valor" stroke={COLORES[0]} strokeWidth={2} />
                        </LineChart>
                      ) : grafico.tipo === 'Area' ? (
                        <AreaChart data={data}>
                          <CartesianGrid strokeDasharray="3 3" opacity={0.3} />
                          <XAxis dataKey="campoX" fontSize={11} />
                          <YAxis fontSize={11} tickFormatter={(v: number) => v.toLocaleString('es-AR')} />
                          <Tooltip formatter={((v: unknown) => typeof v === 'number' ? v.toLocaleString('es-AR') : String(v)) as never} />
                          <Area type="monotone" dataKey="valor" stroke={COLORES[0]} fill={COLORES[0]} fillOpacity={0.2} />
                        </AreaChart>
                      ) : (
                        <BarChart data={data}>
                          <CartesianGrid strokeDasharray="3 3" opacity={0.3} />
                          <XAxis dataKey="campoX" fontSize={11} />
                          <YAxis fontSize={11} tickFormatter={(v: number) => v.toLocaleString('es-AR')} />
                          <Tooltip formatter={((v: unknown) => typeof v === 'number' ? v.toLocaleString('es-AR') : String(v)) as never} />
                          <Bar
                            dataKey="valor"
                            fill={COLORES[0]}
                            onClick={((d: { payload?: { campoX?: string } }) => {
                              if (d?.payload?.campoX) setChips((prev) => [...prev.filter((c) => c.campo !== grafico.datos.columnas[0]), { campo: grafico.datos.columnas[0], valor: String(d.payload!.campoX) }]);
                            }) as never}
                            cursor="pointer"
                          />
                        </BarChart>
                      )}
                    </ResponsiveContainer>
                  </div>
                  {grafico.tipo !== 'Torta' && grafico.tipo !== 'Dona' && (
                    <p className="mt-1 text-[10px] text-ink-muted">Hacé clic en una barra para filtrar todo el dashboard por ese valor.</p>
                  )}
                </div>
              ))}
            </div>
          )}

          {resultado.tablas.length > 0 && (
            <div className="grid grid-cols-1 gap-3">
              {resultado.tablas.map((t, i) => (
                <div key={i} className="card">
                  <h3 className="mb-2 text-sm font-semibold">{t.titulo}</h3>
                  <div className="max-h-96 overflow-auto">
                    <table className="w-full text-left text-xs">
                      <thead>
                        <tr className="border-b border-soft text-[10px] uppercase tracking-wide text-ink-muted">
                          {t.datos.columnas.map((c) => <th key={c} className="py-1 pr-3">{c}</th>)}
                        </tr>
                      </thead>
                      <tbody>
                        {t.datos.filas.map((f, j) => (
                          <tr key={j} className="border-b border-soft last:border-0">
                            {t.datos.columnas.map((c) => (
                              <td key={c} className="py-1 pr-3">
                                {f[c] instanceof Date ? (f[c] as Date).toLocaleString('es-AR') : String(f[c] ?? '')}
                              </td>
                            ))}
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              ))}
            </div>
          )}
        </>
      )}
    </div>
  );
}
