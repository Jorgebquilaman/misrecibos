import { useEffect, useState } from 'react';
import { Download, RefreshCw } from 'lucide-react';
import { fichadasApi } from '../api';
import type { MisFichadasDto } from '../types';
import { descargarBlob, formatFecha, mesActual } from '../utils';

export default function FichadasPage() {
  const [datos, setDatos] = useState<MisFichadasDto | null>(null);
  const [anio, setAnio] = useState(mesActual().anio);
  const [mes, setMes] = useState(mesActual().mes);
  const [exportando, setExportando] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [cargando, setCargando] = useState(true);

  const cargar = async (a: number, m: number) => {
    setCargando(true);
    setError(null);
    try {
      const d = await fichadasApi.mias(a, m);
      setDatos(d);
    } catch (e: any) {
      const status = e.response?.status;
      const msg = e.response?.data?.error ?? 'No se pudo consultar el reloj (MSSQL).';
      setError(status === 503 ? `${msg} Reintente en unos minutos.` : msg);
      setDatos(null);
    } finally {
      setCargando(false);
    }
  };

  useEffect(() => {
    cargar(anio, mes);
  }, [anio, mes]);

  const exportar = async (formato: 'pdf' | 'xlsx') => {
    setExportando(true);
    setError(null);
    try {
      const blob = await fichadasApi.exportar(anio, mes, formato);
      descargarBlob(blob, `Fichadas_${anio}_${String(mes).padStart(2, '0')}.${formato}`);
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo exportar el reporte.');
    } finally {
      setExportando(false);
    }
  };

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold">Mis fichadas</h1>
          <p className="text-sm text-ink-secondary">Lectura en vivo del reloj biométrico (MSSQL) — entrada/salida por día.</p>
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
          <button onClick={() => exportar('pdf')} disabled={exportando} className="btn-primary">
            <Download size={16} /> {exportando ? 'Generando...' : 'PDF'}
          </button>
          <button onClick={() => exportar('xlsx')} disabled={exportando} className="btn-secondary">
            <Download size={16} /> Excel
          </button>
        </div>
      </div>

      {error && (
        <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg tint-danger px-3 py-2 text-sm">
          <span>{error}</span>
          <button onClick={() => cargar(anio, mes)} className="btn-secondary !py-1 text-xs">
            <RefreshCw size={14} /> Reintentar
          </button>
        </div>
      )}

      {cargando && !datos && !error && <p className="text-sm text-ink-secondary">Cargando fichadas del reloj...</p>}

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