import { useEffect, useState } from 'react';
import {
  Area, AreaChart, Bar, BarChart, CartesianGrid, Cell, Legend, Line, LineChart,
  Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis
} from 'recharts';
import { adminApi } from '../../api';

const COLOR_PRESENCIA = '#8fb996';
const COLOR_AUSENCIA = '#d98e88';

const tooltipStyle = {
  contentStyle: {
    background: 'var(--chart-tooltip-bg)',
    border: '1px solid var(--chart-tooltip-border)',
    borderRadius: 14,
    color: 'var(--text-primary)'
  },
  cursor: { fill: 'var(--chart-cursor)' }
};

const eje = { fontSize: 12, tick: { fill: 'var(--chart-tick)' }, axisLine: false, tickLine: false } as const;

function Kpi({ etiqueta, valor, detalle }: { etiqueta: string; valor: string; detalle?: string }) {
  return (
    <div className="card">
      <p className="text-xs uppercase tracking-wide text-ink-secondary">{etiqueta}</p>
      <p className="mt-1 text-2xl font-bold">{valor}</p>
      {detalle && <p className="text-xs text-ink-secondary">{detalle}</p>}
    </div>
  );
}

export default function AdminEstadisticasPage() {
  const hoy = new Date();
  const inicioMes = new Date(hoy.getFullYear(), hoy.getMonth(), 1);
  const [desde, setDesde] = useState(inicioMes.toISOString().slice(0, 10));
  const [hasta, setHasta] = useState(hoy.toISOString().slice(0, 10));
  const [datos, setDatos] = useState<any>(null);
  const [fichadas, setFichadas] = useState<any>(null);
  const [generales, setGenerales] = useState<any>(null);
  const [cargandoGenerales, setCargandoGenerales] = useState(true);
  const [solapa, setSolapa] = useState<'general' | 'horarios'>('horarios');

  useEffect(() => {
    adminApi.estadisticasAccesos(desde, hasta).then(setDatos).catch(() => {});
    adminApi.estadisticasFichadas(desde, hasta).then(setFichadas).catch(() => {});
  }, [desde, hasta]);

  useEffect(() => {
    setCargandoGenerales(true);
    adminApi.estadisticasGenerales().then(setGenerales).catch(() => {}).finally(() => setCargandoGenerales(false));
  }, []);

  const datosChart = datos?.porSemana?.map((d: any) => ({ semana: d.semana, accesos: d.cantidad })) ?? [];

  const asistenciaPorDia = fichadas?.porDia?.map((d: any) => ({
    fecha: d.fecha.slice(5).split('-').reverse().join('/'),
    Asistencia: d.presentes,
    Ausencias: d.ausentes
  })) ?? [];

  const porDiaSemana = fichadas?.porDiaSemana ?? [];
  const porFranja = fichadas?.porFranja?.map((f: any) => ({ franja: f.franja, Entradas: f.entradas, Salidas: f.salidas })) ?? [];
  const porArea = fichadas?.porArea ?? [];
  const topEmpleados = fichadas?.topEmpleados ?? [];
  const topTardanzas = fichadas?.topTardanzas ?? [];
  const resumen = fichadas?.resumen;

  const horas = (h: number) => `${Math.floor(h)}h ${Math.round((h % 1) * 60)}m`;

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Estadísticas</h1>
        <p className="text-sm text-ink-secondary">
          Asistencia del personal (datos en vivo del reloj biométrico) y actividad en el portal.
        </p>
      </div>

      <div className="card flex flex-wrap items-end gap-3">
        <div>
          <label className="label">Desde</label>
          <input type="date" value={desde} onChange={(e) => setDesde(e.target.value)} className="input !w-auto" />
        </div>
        <div>
          <label className="label">Hasta</label>
          <input type="date" value={hasta} onChange={(e) => setHasta(e.target.value)} className="input !w-auto" />
        </div>
      </div>

      <div className="flex gap-2">
        {(['general', 'horarios'] as const).map((s) => (
          <button
            key={s}
            onClick={() => setSolapa(s)}
            className={`btn !px-4 !py-1.5 text-sm ${solapa === s ? 'bg-accent text-accent-ink' : 'btn-secondary'}`}
          >
            {s === 'general' ? 'Vista general' : 'Horarios de ingreso / egreso'}
          </button>
        ))}
      </div>

      {solapa === 'general' && cargandoGenerales && !generales && (
        <div className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {[1, 2, 3, 4].map((i) => (
              <div key={i} className="card animate-pulse">
                <div className="h-4 w-24 rounded bg-surface-soft" />
                <div className="mt-3 h-8 w-16 rounded bg-surface-soft" />
                <div className="mt-2 h-3 w-32 rounded bg-surface-soft" />
              </div>
            ))}
          </div>
          <div className="card flex flex-col items-center gap-3 py-10">
            <div className="h-10 w-10 animate-spin rounded-full border-4 border-soft border-t-accent" />
            <p className="animate-pulse text-sm text-ink-secondary">Cargando estadísticas generales...</p>
            <div className="flex gap-1">
              <span className="h-2 w-2 animate-bounce rounded-full bg-accent" style={{ animationDelay: '0ms' }} />
              <span className="h-2 w-2 animate-bounce rounded-full bg-accent" style={{ animationDelay: '150ms' }} />
              <span className="h-2 w-2 animate-bounce rounded-full bg-accent" style={{ animationDelay: '300ms' }} />
            </div>
          </div>
          <div className="grid gap-4 lg:grid-cols-3">
            {[1, 2, 3].map((i) => (
              <div key={i} className="card animate-pulse">
                <div className="h-5 w-32 rounded bg-surface-soft" />
                <div className="mt-4 h-48 rounded bg-surface-soft" />
              </div>
            ))}
          </div>
        </div>
      )}

      {solapa === 'horarios' && resumen && (
        <>
          <h2 className="text-lg font-semibold">Horarios de ingreso y egreso</h2>
          <p className="text-sm text-ink-secondary">Análisis de presentes/ausentes, días con mayor concurrencia y distribución horaria (datos del reloj MSSQL).</p>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4 xl:grid-cols-6">
            <Kpi etiqueta="Hora prom. ingreso" valor={resumen.horaPromedioIngreso ?? '—'} detalle="Promedio de entradas" />
            <Kpi etiqueta="Hora prom. egreso" valor={resumen.horaPromedioEgreso ?? '—'} detalle="Promedio de salidas" />
            <Kpi etiqueta="Asistencia prom." valor={`${resumen.promedioAsistenciaPct}%`}
              detalle="Presentes / habituales por día" />
            <Kpi etiqueta="Empleados con fichadas" valor={String(resumen.empleadosConMarcas)}
              detalle={`${resumen.diasLaborables} días laborables`} />
            <Kpi etiqueta="Horas totales" valor={`${Math.round(resumen.totalHoras)} h`} />
            <Kpi etiqueta="Tardanzas" valor={String(resumen.tardanzas)} detalle="Entradas posteriores a 08:30" />
            <Kpi etiqueta="Día más concurrido" valor={resumen.diaMasConcurrido ?? '—'}
              detalle={resumen.diaMenosConcurrido ? `Menos: ${resumen.diaMenosConcurrido}` : undefined} />
            <Kpi etiqueta="Horario pico" valor={resumen.franjaPico ?? '—'} detalle="Mayor cantidad de entradas" />
          </div>

          <div className="card">
            <h3 className="mb-3 font-semibold">Asistencia vs ausencias por día</h3>
            <ResponsiveContainer width="100%" height={300}>
              <AreaChart data={asistenciaPorDia}>
                <defs>
                  <linearGradient id="gPresencia" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor={COLOR_PRESENCIA} stopOpacity={0.5} />
                    <stop offset="100%" stopColor={COLOR_PRESENCIA} stopOpacity={0.05} />
                  </linearGradient>
                  <linearGradient id="gAusencia" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor={COLOR_AUSENCIA} stopOpacity={0.4} />
                    <stop offset="100%" stopColor={COLOR_AUSENCIA} stopOpacity={0.03} />
                  </linearGradient>
                </defs>
                <CartesianGrid stroke="var(--chart-grid)" strokeDasharray="3 3" />
                <XAxis dataKey="fecha" {...eje} />
                <YAxis allowDecimals={false} {...eje} />
                <Tooltip {...tooltipStyle} />
                <Legend />
                <Area type="monotone" dataKey="Asistencia" stroke={COLOR_PRESENCIA} fill="url(#gPresencia)" strokeWidth={2} />
                <Area type="monotone" dataKey="Ausencias" stroke={COLOR_AUSENCIA} fill="url(#gAusencia)" strokeWidth={2} />
              </AreaChart>
            </ResponsiveContainer>
          </div>

          <div className="grid gap-4 lg:grid-cols-2">
            <div className="card">
              <h3 className="mb-3 font-semibold">Concurrencia por día de la semana</h3>
              <p className="mb-2 text-xs text-ink-secondary">Promedio de presentes por semana en cada día.</p>
              <ResponsiveContainer width="100%" height={260}>
                <BarChart data={porDiaSemana}>
                  <CartesianGrid stroke="var(--chart-grid)" strokeDasharray="3 3" />
                  <XAxis dataKey="dia" {...eje} />
                  <YAxis allowDecimals={false} {...eje} />
                  <Tooltip {...tooltipStyle}
                    formatter={(v: any, _n: any, p: any) => [`${v} presentes (total ${p.payload.totalPresentes})`, 'Promedio']} />
                  <Bar dataKey="promedio" name="Promedio" fill="var(--chart-fill)" radius={[6, 6, 0, 0]}>
                    {porDiaSemana.map((d: any, i: number) => (
                      <Cell key={i} fill={d.promedio === Math.max(...porDiaSemana.map((x: any) => x.promedio))
                        ? COLOR_PRESENCIA : 'var(--chart-fill)'} />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            </div>

            <div className="card">
              <h3 className="mb-3 font-semibold">Ingreso vs egreso por franja horaria</h3>
              <p className="mb-2 text-xs text-ink-secondary">Comparativa de entradas y salidas por hora (05:00–20:00).</p>
              <ResponsiveContainer width="100%" height={260}>
                <BarChart data={porFranja}>
                  <CartesianGrid stroke="var(--chart-grid)" strokeDasharray="3 3" />
                  <XAxis dataKey="franja" {...eje} interval={1} />
                  <YAxis allowDecimals={false} {...eje} />
                  <Tooltip {...tooltipStyle} />
                  <Legend />
                  <Bar dataKey="Entradas" fill={COLOR_PRESENCIA} radius={[4, 4, 0, 0]} />
                  <Bar dataKey="Salidas" fill={COLOR_AUSENCIA} radius={[4, 4, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          </div>

          <div className="grid gap-4 lg:grid-cols-2">
            <div className="card">
              <h3 className="mb-3 font-semibold">Asistencia promedio por área</h3>
              <ResponsiveContainer width="100%" height={Math.max(200, porArea.length * 34)}>
                <BarChart data={porArea} layout="vertical" margin={{ left: 30 }}>
                  <CartesianGrid stroke="var(--chart-grid)" strokeDasharray="3 3" horizontal={false} />
                  <XAxis type="number" domain={[0, 100]} unit="%" {...eje} />
                  <YAxis type="category" dataKey="area" width={140} {...eje} />
                  <Tooltip {...tooltipStyle}
                    formatter={(v: any, _n: any, p: any) => [`${v}% (${p.payload.empleados} empleados)`, 'Asistencia']} />
                  <Bar dataKey="asistenciaPct" name="Asistencia" fill={COLOR_PRESENCIA} radius={[0, 6, 6, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>

            <div className="card">
              <h3 className="mb-3 font-semibold">Mayores tardanzas</h3>
              <p className="mb-2 text-xs text-ink-secondary">Entradas posteriores a las 08:30.</p>
              <ul className="space-y-2">
                {topTardanzas.map((t: any) => (
                  <li key={t.legajo} className="flex items-center justify-between text-sm">
                    <span>{t.nombre} <span className="text-ink-secondary">(legajo {t.legajo})</span></span>
                    <span className="rounded-full bg-red-500/10 px-2 py-0.5 font-semibold text-red-400">
                      {t.tardanzas}
                    </span>
                  </li>
                ))}
                {!topTardanzas.length && <p className="text-sm text-ink-secondary">Sin tardanzas en el período. 🎉</p>}
              </ul>
            </div>
          </div>

          <div className="card overflow-x-auto">
            <h3 className="mb-3 font-semibold">Top 10 de empleados por horas trabajadas</h3>
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs uppercase tracking-wide text-ink-secondary">
                  <th className="pb-2 pr-4">Empleado</th>
                  <th className="pb-2 pr-4">Legajo</th>
                  <th className="pb-2 pr-4">Días</th>
                  <th className="pb-2 pr-4">Horas totales</th>
                  <th className="pb-2">Promedio diario</th>
                </tr>
              </thead>
              <tbody>
                {topEmpleados.map((t: any) => (
                  <tr key={t.legajo} className="border-t border-white/5">
                    <td className="py-2 pr-4 font-medium">{t.nombre}</td>
                    <td className="py-2 pr-4">{t.legajo}</td>
                    <td className="py-2 pr-4">{t.dias}</td>
                    <td className="py-2 pr-4">{horas(t.horas)}</td>
                    <td className="py-2">{horas(t.promedioHoras)}</td>
                  </tr>
                ))}
                {!topEmpleados.length && (
                  <tr><td colSpan={5} className="py-3 text-ink-secondary">Sin marcas en el período.</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </>
      )}

      {solapa === 'general' && <h2 className="text-lg font-semibold">Actividad en el portal</h2>}
      {solapa === 'general' && datos && (
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="card">
            <h3 className="mb-3 font-semibold">Accesos por semana</h3>
            <ResponsiveContainer width="100%" height={280}>
              <BarChart data={datosChart}>
                <CartesianGrid stroke="var(--chart-grid)" strokeDasharray="3 3" />
                <XAxis dataKey="semana" {...eje} />
                <YAxis allowDecimals={false} {...eje} />
                <Tooltip {...tooltipStyle} />
                <Bar dataKey="accesos" name="Accesos" fill="var(--chart-fill)" radius={[6, 6, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>
          <div className="card">
            <h3 className="mb-3 font-semibold">Accesos por acción</h3>
            <ul className="space-y-2">
              {datos.porAccion?.map((a: any) => (
                <li key={a.accion} className="flex items-center justify-between text-sm">
                  <span className="capitalize">{a.accion.replace(/([A-Z])/g, ' $1').toLowerCase()}</span>
                  <span className="font-semibold">{a.cantidad}</span>
                </li>
              ))}
              {!datos.porAccion?.length && <p className="text-sm text-ink-secondary">Sin actividad.</p>}
            </ul>
          </div>
        </div>
      )}

      {solapa === 'general' && generales && (
        <>
          <h2 className="text-lg font-semibold">Vista general institucional</h2>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <Kpi etiqueta="Empleados totales" valor={String(generales.empleados.total)} detalle={`${generales.empleados.activos} activos · ${generales.empleados.inactivos} inactivos`} />
            <Kpi etiqueta="Períodos / Activos" valor={`${generales.recibos.totalPeriodos} / ${generales.recibos.periodosActivos}`} detalle={`${generales.recibos.totalDescargas} descargas totales`} />
            <Kpi etiqueta="Licencias totales" valor={String(generales.licencias.total)} detalle={`Últimos 6 meses: ${generales.licencias.ultimos6Meses?.reduce((a: number, b: any) => a + b.cantidad, 0) ?? 0}`} />
            <Kpi etiqueta="CV completitud" valor={`${Math.round(generales.cv.completitudPromedio)}%`} detalle={`${generales.cv.totalCertificados} certificados · ${generales.cv.totalExperiencias} experiencias`} />
          </div>

          <div className="grid gap-4 lg:grid-cols-3">
            <div className="card">
              <h3 className="mb-2 font-semibold">Empleados por área</h3>
              <ResponsiveContainer width="100%" height={260}>
                <PieChart>
                  <Pie data={generales.empleados.porArea} dataKey="cantidad" nameKey="nombre" cx="50%" cy="50%" outerRadius={90} label={({ nombre, cantidad }: any) => `${nombre}: ${cantidad}`}>
                    {generales.empleados.porArea.map((_: any, i: number) => <Cell key={i} fill={['#8fb996', '#7aa8c2', '#d9b88e', '#d98e88', '#a8a0c8', '#f2c14e', '#88b0a6', '#c49ab8'][i % 8]} />)}
                  </Pie>
                  <Tooltip {...tooltipStyle} />
                </PieChart>
              </ResponsiveContainer>
            </div>
            <div className="card">
              <h3 className="mb-2 font-semibold">Distribución por rol</h3>
              <ResponsiveContainer width="100%" height={260}>
                <PieChart>
                  <Pie data={generales.empleados.porRol} dataKey="cantidad" nameKey="nombre" cx="50%" cy="50%" outerRadius={90} label>
                    {generales.empleados.porRol.map((_: any, i: number) => <Cell key={i} fill={['#6366f1', '#10b981', '#f59e0b', '#ef4444', '#06b6d4', '#8b5cf6'][i % 6]} />)}
                  </Pie>
                  <Tooltip {...tooltipStyle} /><Legend />
                </PieChart>
              </ResponsiveContainer>
            </div>
            <div className="card">
              <h3 className="mb-2 font-semibold">Anuncios por tipo</h3>
              <ResponsiveContainer width="100%" height={260}>
                <PieChart>
                  <Pie data={generales.anuncios.porTipo} dataKey="cantidad" nameKey="nombre" cx="50%" cy="50%" outerRadius={80}>
                    {generales.anuncios.porTipo.map((_: any, i: number) => <Cell key={i} fill={['#f59e0b', '#3b82f6', '#10b981'][i % 3]} />)}
                  </Pie>
                  <Tooltip {...tooltipStyle} /><Legend />
                </PieChart>
              </ResponsiveContainer>
              <div className="mt-2 flex flex-wrap gap-2 text-xs">
                {generales.anuncios.porPrioridad.map((p: any) => <span key={p.nombre} className="rounded-pill bg-surface-soft px-2 py-1">{p.nombre}: {p.cantidad}</span>)}
              </div>
            </div>
          </div>

          <div className="grid gap-4 lg:grid-cols-2">
            <div className="card">
              <h3 className="mb-2 font-semibold">Licencias por estado</h3>
              <ResponsiveContainer width="100%" height={260}>
                <PieChart>
                  <Pie data={generales.licencias.porEstado} dataKey="cantidad" nameKey="nombre" cx="50%" cy="50%" outerRadius={85} label>
                    {generales.licencias.porEstado.map((_: any, i: number) => <Cell key={i} fill={['#f59e0b', '#10b981', '#ef4444', '#6b7280'][i % 4]} />)}
                  </Pie>
                  <Tooltip {...tooltipStyle} /><Legend />
                </PieChart>
              </ResponsiveContainer>
            </div>
            <div className="card">
              <h3 className="mb-2 font-semibold">Licencias por tipo</h3>
              <ResponsiveContainer width="100%" height={260}>
                <BarChart data={generales.licencias.porTipo} layout="vertical" margin={{ left: 40 }}>
                  <CartesianGrid stroke="var(--chart-grid)" strokeDasharray="3 3" horizontal={false} />
                  <XAxis type="number" allowDecimals={false} {...eje} />
                  <YAxis type="category" dataKey="nombre" width={140} {...eje} />
                  <Tooltip {...tooltipStyle} />
                  <Bar dataKey="cantidad" fill="#f59e0b" radius={[0, 6, 6, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          </div>

          <div className="card">
            <h3 className="mb-2 font-semibold">Licencias — tendencia últimos 6 meses</h3>
            <ResponsiveContainer width="100%" height={220}>
              <AreaChart data={generales.licencias.ultimos6Meses}>
                <CartesianGrid stroke="var(--chart-grid)" strokeDasharray="3 3" />
                <XAxis dataKey="mes" {...eje} />
                <YAxis allowDecimals={false} {...eje} />
                <Tooltip {...tooltipStyle} />
                <Area type="monotone" dataKey="cantidad" stroke="#f59e0b" fill="#fef3c7" strokeWidth={2} />
              </AreaChart>
            </ResponsiveContainer>
          </div>

          <div className="grid gap-4 lg:grid-cols-2">
            <div className="card">
              <h3 className="mb-2 font-semibold">Recibos — descargas por período (top 10)</h3>
              <ResponsiveContainer width="100%" height={280}>
                <BarChart data={generales.recibos.porPeriodo}>
                  <CartesianGrid stroke="var(--chart-grid)" strokeDasharray="3 3" />
                  <XAxis dataKey="nombre" {...eje} interval={0} angle={-20} textAnchor="end" height={60} />
                  <YAxis allowDecimals={false} {...eje} />
                  <Tooltip {...tooltipStyle} />
                  <Bar dataKey="cantidad" fill="#0f766e" radius={[6, 6, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
            <div className="card">
              <h3 className="mb-2 font-semibold">Recibos — descargas por mes</h3>
              <ResponsiveContainer width="100%" height={280}>
                <LineChart data={generales.recibos.porMes}>
                  <CartesianGrid stroke="var(--chart-grid)" strokeDasharray="3 3" />
                  <XAxis dataKey="mes" {...eje} />
                  <YAxis allowDecimals={false} {...eje} />
                  <Tooltip {...tooltipStyle} />
                  <Line type="monotone" dataKey="cantidad" stroke="#0f766e" strokeWidth={2} dot={{ r: 3 }} />
                </LineChart>
              </ResponsiveContainer>
            </div>
          </div>

          <div className="grid gap-4 lg:grid-cols-3">
            <div className="card">
              <h3 className="mb-2 font-semibold">CV — certificados por tipo</h3>
              <ResponsiveContainer width="100%" height={240}>
                <PieChart>
                  <Pie data={generales.cv.certificadosPorTipo} dataKey="cantidad" nameKey="nombre" cx="50%" cy="50%" outerRadius={75} label>
                    {generales.cv.certificadosPorTipo.map((_: any, i: number) => <Cell key={i} fill={['#06b6d4', '#8b5cf6', '#f59e0b', '#10b981', '#ef4444'][i % 5]} />)}
                  </Pie>
                  <Tooltip {...tooltipStyle} />
                </PieChart>
              </ResponsiveContainer>
              <p className="text-center text-xs text-ink-secondary">{generales.cv.totalCertificados} certificados totales · {generales.cv.completitudPromedio.toFixed(1)}% completitud promedio</p>
            </div>
            <div className="card">
              <h3 className="mb-2 font-semibold">CV — certificados por estado</h3>
              <ResponsiveContainer width="100%" height={240}>
                <PieChart>
                  <Pie data={generales.cv.certificadosPorEstado} dataKey="cantidad" nameKey="nombre" cx="50%" cy="50%" outerRadius={75}>
                    {generales.cv.certificadosPorEstado.map((_: any, i: number) => <Cell key={i} fill={['#f59e0b', '#10b981', '#ef4444'][i % 3]} />)}
                  </Pie>
                  <Tooltip {...tooltipStyle} /><Legend />
                </PieChart>
              </ResponsiveContainer>
            </div>
            <div className="card">
              <h3 className="mb-2 font-semibold">CV — completitud por rangos</h3>
              <ResponsiveContainer width="100%" height={240}>
                <BarChart data={generales.cv.completitudRangos}>
                  <CartesianGrid stroke="var(--chart-grid)" strokeDasharray="3 3" />
                  <XAxis dataKey="nombre" {...eje} />
                  <YAxis allowDecimals={false} {...eje} />
                  <Tooltip {...tooltipStyle} />
                  <Bar dataKey="cantidad" fill="#6366f1" radius={[6, 6, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
              <p className="text-center text-xs text-ink-secondary">Experiencias: {generales.cv.totalExperiencias} · Antecedentes: {generales.cv.totalAntecedentes}</p>
            </div>
          </div>

          <div className="grid gap-4 lg:grid-cols-2">
            <div className="card">
              <h3 className="mb-2 font-semibold">Certificados laborales por tipo</h3>
              {generales.certificados.porTipo.length ? (
                <ResponsiveContainer width="100%" height={240}>
                  <PieChart>
                    <Pie data={generales.certificados.porTipo} dataKey="cantidad" nameKey="nombre" cx="50%" cy="50%" outerRadius={80} label>
                      {generales.certificados.porTipo.map((_: any, i: number) => <Cell key={i} fill={['#10b981', '#3b82f6'][i % 2]} />)}
                    </Pie>
                    <Tooltip {...tooltipStyle} /><Legend />
                  </PieChart>
                </ResponsiveContainer>
              ) : <p className="text-sm text-ink-secondary">Sin certificados laborales.</p>}
              <p className="text-center text-xs text-ink-secondary">Total: {generales.certificados.total}</p>
            </div>
            <div className="card">
              <h3 className="mb-2 font-semibold">Anuncios — alcance y prioridad</h3>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <p className="mb-1 text-xs font-medium">Por alcance</p>
                  <ul className="space-y-1 text-sm">
                    {generales.anuncios.porAlcance.map((a: any) => <li key={a.nombre} className="flex justify-between"><span>{a.nombre}</span><b>{a.cantidad}</b></li>)}
                  </ul>
                </div>
                <div>
                  <p className="mb-1 text-xs font-medium">Por prioridad</p>
                  <ul className="space-y-1 text-sm">
                    {generales.anuncios.porPrioridad.map((a: any) => <li key={a.nombre} className="flex justify-between"><span>{a.nombre}</span><b>{a.cantidad}</b></li>)}
                  </ul>
                </div>
              </div>
              <p className="mt-3 text-center text-xs text-ink-secondary">Total anuncios: {generales.anuncios.total}</p>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
