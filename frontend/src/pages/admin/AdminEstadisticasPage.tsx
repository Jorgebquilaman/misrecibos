import { useEffect, useState } from 'react';
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { adminApi } from '../../api';

export default function AdminEstadisticasPage() {
  const hoy = new Date();
  const inicioMes = new Date(hoy.getFullYear(), hoy.getMonth(), 1);
  const [desde, setDesde] = useState(inicioMes.toISOString().slice(0, 10));
  const [hasta, setHasta] = useState(hoy.toISOString().slice(0, 10));
  const [datos, setDatos] = useState<any>(null);

  const cargar = () => {
    adminApi.estadisticasAccesos(desde, hasta).then(setDatos).catch(() => {});
  };

  useEffect(() => { cargar(); }, [desde, hasta]);

  const datosChart =
    datos?.porSemana?.map((d: any) => ({ semana: d.semana, accesos: d.cantidad })) ?? [];

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Estadísticas de accesos</h1>
        <p className="text-sm text-ink-secondary">Actividad de los empleados en el portal.</p>
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
        <button onClick={cargar} className="btn-secondary">Actualizar</button>
      </div>

      {datos && (
        <>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="card">
              <h2 className="mb-3 font-semibold">Accesos por semana</h2>
              <ResponsiveContainer width="100%" height={280}>
                <BarChart data={datosChart}>
                  <CartesianGrid stroke="var(--chart-grid)" strokeDasharray="3 3" />
                  <XAxis dataKey="semana" fontSize={12} tick={{ fill: 'var(--chart-tick)' }} axisLine={false} tickLine={false} />
                  <YAxis allowDecimals={false} fontSize={12} tick={{ fill: 'var(--chart-tick)' }} axisLine={false} tickLine={false} />
                  <Tooltip
                    contentStyle={{ background: 'var(--chart-tooltip-bg)', border: '1px solid var(--chart-tooltip-border)', borderRadius: 14, color: 'var(--text-primary)' }}
                    cursor={{ fill: 'var(--chart-cursor)' }}
                  />
                  <Bar dataKey="accesos" name="Accesos" fill="var(--chart-fill)" radius={[6, 6, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
            <div className="card">
              <h2 className="mb-3 font-semibold">Accesos por acción</h2>
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
        </>
      )}
    </div>
  );
}