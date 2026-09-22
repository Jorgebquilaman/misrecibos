import { useEffect, useState } from 'react';
import { Gauge, Pencil, Plus, Trash2 } from 'lucide-react';
import { dashboardsApi } from '../../api';
import type { DashboardCompletoDto, DashboardResumenDto } from '../../types';
import DashboardsBuilder from '../../components/DashboardsBuilder';
import DashboardViewer from '../../components/DashboardViewer';

export default function AdminDashboardsPage() {
  const [dashboards, setDashboards] = useState<DashboardResumenDto[]>([]);
  const [vista, setVista] = useState<{ tipo: 'builder' | 'viewer'; dashboard: DashboardCompletoDto | null } | null>(null);
  const [cargando, setCargando] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const cargar = () => dashboardsApi.todos().then(setDashboards).catch(() => {});
  useEffect(() => { cargar(); }, []);

  if (vista) {
    return (
      <div className="mx-auto max-w-6xl space-y-4">
        <h2 className="text-lg font-semibold">
          {vista.tipo === 'builder' ? 'Editor' : 'Vista previa'} — {vista.dashboard?.nombre ?? 'Nuevo dashboard'}
        </h2>
        {vista.tipo === 'builder' ? (
          <DashboardsBuilder
            dashboard={vista.dashboard}
            onGuardado={async (idRecien) => {
              setVista({ tipo: 'viewer', dashboard: await dashboardsApi.porId(idRecien) });
            }}
            onCerrar={() => { setVista(null); cargar(); }}
          />
        ) : (
          <div className="space-y-3">
            <button onClick={() => setVista({ tipo: 'builder', dashboard: vista.dashboard })} className="btn-secondary text-sm">
              ← Volver al editor
            </button>
            {vista.dashboard && <DashboardViewer dashboard={vista.dashboard} />}
          </div>
        )}
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Dashboards</h1>
        <p className="text-sm text-ink-secondary">
          Paneles visuales con KPIs, KPOs, gráficos y filtros globales que se aplican a todos los controles.
          Los ven Rrhh y Administradores en el menú Dashboards.
        </p>
      </div>

      <div className="flex justify-end">
        <button onClick={() => setVista({ tipo: 'builder', dashboard: null })} className="btn-primary flex items-center gap-2">
          <Plus size={14} /> Nuevo dashboard
        </button>
      </div>

      {error && <p className="text-sm text-danger">{error}</p>}

      {dashboards.length === 0 ? (
        <p className="text-sm text-ink-secondary">No hay dashboards todavía.</p>
      ) : (
        <div className="space-y-2">
          {dashboards.map((d) => (
            <div key={d.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-soft p-3">
              <div className="min-w-0">
                <p className="flex items-center gap-2 font-medium">
                  <Gauge size={15} /> {d.nombre}
                  {!d.activo && <span className="badge tint-warning">inactivo</span>}
                </p>
                {d.descripcion && <p className="text-xs text-ink-secondary">{d.descripcion}</p>}
              </div>
              <div className="flex items-center gap-1">
                <button
                  onClick={async () => setVista({ tipo: 'viewer', dashboard: await dashboardsApi.porId(d.id) })}
                  className="btn-secondary !px-2 !py-1 text-xs"
                >Ver</button>
                {d.puedeEditar && (
                  <button
                    onClick={async () => setVista({ tipo: 'builder', dashboard: await dashboardsApi.porId(d.id) })}
                    className="btn-secondary !px-2 !py-1" title="Editar"
                  ><Pencil size={14} /></button>
                )}
                {d.puedeEditar && (
                  <button
                    onClick={() => {
                      if (!confirm(`¿Eliminar el dashboard "${d.nombre}"?`)) return;
                      setCargando(true);
                      dashboardsApi.eliminar(d.id).then(cargar)
                        .catch((e) => setError(e.response?.data?.error)).finally(() => setCargando(false));
                    }}
                    className="btn-danger !px-2 !py-1" title="Eliminar"
                  ><Trash2 size={14} /></button>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {cargando && <p className="text-sm text-ink-secondary">Trabajando…</p>}
    </div>
  );
}
