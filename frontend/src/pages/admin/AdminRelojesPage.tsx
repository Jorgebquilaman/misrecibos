import RelojesZkAdmin from '../../components/RelojesZkAdmin';

export default function AdminRelojesPage() {
  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Relojes ZKTeco</h1>
        <p className="text-sm text-ink-secondary">
          Descarga de marcas de asistencia directo desde los relojes biométricos ZKTeco por red.
          Probá la conexión, descargá las marcas cuando necesites y vaciá la memoria del equipo si se llena.
        </p>
      </div>
      <RelojesZkAdmin />
    </div>
  );
}
