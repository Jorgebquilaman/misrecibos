import { useEffect, useState } from 'react';
import { Gauge } from 'lucide-react';
import { dashboardsApi } from '../api';
import type { DashboardCompletoDto, DashboardResumenDto } from '../types';
import DashboardViewer from '../components/DashboardViewer';

export default function MisDashboardsPage() {
  const [dashboards, setDashboards] = useState<DashboardResumenDto[]>([]);
  const [abierto, setAbierto] = useState<DashboardCompletoDto | null>(null);

  useEffect(() => { dashboardsApi.todos().then(setDashboards).catch(() => {}); }, []);

  if (abierto) {
    return (
      <div className="mx-auto max-w-6xl space-y-4">
        <button onClick={() => setAbierto(null)} className="btn-secondary text-sm">← Volver</button>
        <DashboardViewer dashboard={abierto} />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Dashboards</h1>
        <p className="text-sm text-ink-secondary">Paneles de indicadores compartidos por el equipo de administración.</p>
      </div>
      {dashboards.length === 0 ? (
        <p className="text-sm text-ink-secondary">Todavía no hay dashboards disponibles.</p>
      ) : (
        <div className="space-y-2">
          {dashboards.map((d) => (
            <button
              key={d.id}
              onClick={async () => setAbierto(await dashboardsApi.porId(d.id))}
              className="flex w-full items-center justify-between rounded-lg border border-soft p-3 text-left hover:bg-black/[0.02]"
            >
              <span className="flex items-center gap-2 font-medium"><Gauge size={15} /> {d.nombre}</span>
              {d.descripcion && <span className="hidden text-xs text-ink-secondary sm:block">{d.descripcion}</span>}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
