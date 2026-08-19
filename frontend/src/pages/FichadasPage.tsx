import { useEffect, useState } from 'react';
import { RefreshCw } from 'lucide-react';
import { fichadasApi } from '../api';
import type { MisFichadasDto } from '../types';
import { formatFecha, mesActual } from '../utils';

export default function FichadasPage() {
  const [datos, setDatos] = useState<MisFichadasDto | null>(null);
  const [anio, setAnio] = useState(mesActual().anio);
  const [mes, setMes] = useState(mesActual().mes);

  useEffect(() => {
    fichadasApi.mias(anio, mes).then(setDatos).catch(() => {});
  }, [anio, mes]);

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold">Mis fichadas</h1>
          <p className="text-sm text-ink-secondary">Marcas de entrada/salida sincronizadas con el reloj.</p>
        </div>
        <div className="flex items-center gap-2">
          <input
            type="month"
            value={`${anio}-${String(mes).padStart(2, '0')}`}
            onChange={(e) => {
              const [a, m] = e.target.value.split('-').map(Number);
              setAnio(a);
              setMes(m);
            }}
            className="input !w-auto"
          />
          <button onClick={() => { setAnio(mesActual().anio); setMes(mesActual().mes); }} className="btn-secondary">
            <RefreshCw size={16} /> Este mes
          </button>
        </div>
      </div>

      {datos && (
        <>
          <div className="grid gap-4 sm:grid-cols-3">
            <div className="card text-center">
              <p className="text-3xl font-bold text-accent-text">{datos.resumen.diasTrabajados}</p>
              <p className="text-sm text-ink-secondary">Días trabajados</p>
            </div>
            <div className="card text-center">
              <p className="text-3xl font-bold text-accent-text">{datos.resumen.horasTotales}</p>
              <p className="text-sm text-ink-secondary">Horas totales</p>
            </div>
            <div className="card text-center">
              <p className={`text-3xl font-bold ${datos.resumen.diasConAnomalia > 0 ? 'text-amber-600' : 'text-accent-text'}`}>
                {datos.resumen.diasConAnomalia}
              </p>
              <p className="text-sm text-ink-secondary">Días con anomalía</p>
            </div>
          </div>

          <div className="card overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="border-b text-ink-secondary">
                  <th className="py-2">Fecha</th>
                  <th className="py-2">Entrada</th>
                  <th className="py-2">Salida</th>
                  <th className="py-2">Horas</th>
                  <th className="py-2"></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-soft">
                {datos.jornadas.map((j) => (
                  <tr key={j.fecha}>
                    <td className="py-2 font-medium">{formatFecha(j.fecha)}</td>
                    <td className="py-2">{j.entrada ? hora(j.entrada) : '—'}</td>
                    <td className="py-2">{j.salida ? hora(j.salida) : '—'}</td>
                    <td className="py-2">{j.horas}</td>
                    <td className="py-2">
                      {j.esAnomalia && <span className="badge tint-warning">Anomalía</span>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            {datos.jornadas.length === 0 && <p className="py-4 text-sm text-ink-secondary">Sin fichadas en el mes seleccionado.</p>}
          </div>
        </>
      )}
    </div>
  );
}

function hora(iso: string): string {
  return new Date(iso).toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit' });
}