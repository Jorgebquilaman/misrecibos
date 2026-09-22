import EdificiosAdmin from '../../components/EdificiosAdmin';

export default function AdminEdificiosPage() {
  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Edificios</h1>
        <p className="text-sm text-ink-secondary">
          Edificios/sedes con coordenadas GPS y radio de detección. Las marcas manuales se asocian
          al edificio más cercano dentro de su radio.
        </p>
      </div>
      <EdificiosAdmin />
    </div>
  );
}
