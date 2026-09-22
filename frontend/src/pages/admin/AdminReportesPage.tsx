import ReportesAdmin from '../../components/ReportesAdmin';

export default function AdminReportesPage() {
  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Reportes</h1>
        <p className="text-sm text-ink-secondary">
          Diseñá reportes visuales sobre consultas SQL: agrupamientos, agregaciones, filtros,
          gráficos y subreportes. Solo lectura, con permisos por usuario y rol.
        </p>
      </div>
      <ReportesAdmin />
    </div>
  );
}
