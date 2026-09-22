import DescargaRelojAdmin from '../../components/DescargaRelojAdmin';

export default function AdminDescargaRelojPage() {
  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Descarga de fichadas</h1>
        <p className="text-sm text-ink-secondary">
          Interfaz de descarga del reloj biométrico: conexión, descarga bajo demanda con detalle
          de cada marca, historial y estado de la descarga automática.
        </p>
      </div>
      <DescargaRelojAdmin />
    </div>
  );
}
