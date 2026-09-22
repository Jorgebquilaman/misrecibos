import { useEffect, useState } from 'react';
import { BarChart3, Eye } from 'lucide-react';
import { reportesApi } from '../api';
import type { ReporteCompletoDto, ReporteResumenDto } from '../types';
import ReporteViewer from '../components/ReporteViewer';

export default function MisReportesPage() {
  const [reportes, setReportes] = useState<ReporteResumenDto[]>([]);
  const [abierto, setAbierto] = useState<ReporteCompletoDto | null>(null);

  useEffect(() => { reportesApi.todos().then(setReportes).catch(() => {}); }, []);

  if (abierto) {
    return (
      <div className="mx-auto max-w-5xl space-y-4">
        <button onClick={() => setAbierto(null)} className="btn-secondary text-sm">← Volver</button>
        <ReporteViewer reporte={abierto} />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Reportes</h1>
        <p className="text-sm text-ink-secondary">Reportes compartidos con vos (por usuario o por rol).</p>
      </div>
      {reportes.length === 0 ? (
        <p className="text-sm text-ink-secondary">Todavía no tenés reportes disponibles.</p>
      ) : (
        <div className="space-y-2">
          {reportes.map((r) => (
            <button
              key={r.id}
              onClick={async () => setAbierto(await reportesApi.porId(r.id))}
              className="flex w-full items-center justify-between rounded-lg border border-soft p-3 text-left hover:bg-black/[0.02] dark:hover:bg-white/[0.03]"
            >
              <span className="flex items-center gap-2 font-medium"><BarChart3 size={15} /> {r.nombre}</span>
              <span className="flex items-center gap-1 text-xs text-blue-600"><Eye size={13} /> ver</span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
