import { useEffect, useState } from 'react';
import { Download, Mail } from 'lucide-react';
import { recibosApi } from '../api';
import type { DescargaReciboDto, ReciboDisponibleDto } from '../types';
import { descargarBlob, formatFechaHora } from '../utils';

export default function RecibosPage() {
  const [disponibles, setDisponibles] = useState<ReciboDisponibleDto[]>([]);
  const [historial, setHistorial] = useState<DescargaReciboDto[]>([]);
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState<string | null>(null);
  const [anio, setAnio] = useState(new Date().getFullYear());

  useEffect(() => {
    recibosApi.disponibles().then(setDisponibles).catch(() => {});
    recibosApi.historial().then(setHistorial).catch(() => {});
  }, []);

  const anioDe = (p: ReciboDisponibleDto): number | null => {
    const m = p.periodoCodigo.match(/^(\d{4})-/);
    if (m) return Number(m[1]);
    const d = p.periodoDescripcion?.match(/\b(19|20)\d{2}\b/);
    return d ? Number(d[0]) : null;
  };

  const anios = Array.from(
    new Set([new Date().getFullYear(), ...disponibles.map((p) => anioDe(p)).filter((a): a is number => a !== null)])
  ).sort((a, b) => b - a);

  const visibles = disponibles.filter((p) => anioDe(p) === null || anioDe(p) === anio);

  const descargar = async (periodoId: string, codigo: string) => {
    setOcupado(periodoId);
    setError(null);
    try {
      const blob = await recibosApi.descargar(periodoId);
      descargarBlob(blob, `Recibo_${codigo}.pdf`);
      setMensaje(`Recibo ${codigo} descargado.`);
      recibosApi.historial().then(setHistorial);
      recibosApi.disponibles().then(setDisponibles);
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo descargar el recibo.');
    } finally {
      setOcupado(null);
    }
  };

  const enviarEmail = async (periodoId: string, codigo: string) => {
    setOcupado(periodoId);
    setError(null);
    try {
      await recibosApi.enviarEmail(periodoId);
      setMensaje(`Recibo ${codigo} enviado a tu correo.`);
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo enviar el recibo por email.');
    } finally {
      setOcupado(null);
    }
  };

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Recibos de sueldo</h1>
        <p className="text-sm text-ink-secondary">Descargá tus recibos o pedí que te los envíen por email.</p>
      </div>

      {mensaje && <p className="rounded-lg tint-success px-3 py-2 text-sm">{mensaje}</p>}
      {error && <p className="rounded-lg tint-danger px-3 py-2 text-sm">{error}</p>}

      <div className="card">
        <div className="mb-3 flex items-center justify-between">
          <h2 className="font-semibold">Períodos disponibles</h2>
          <div className="flex items-center gap-2">
            <label className="text-sm text-ink-secondary">Año</label>
            <select value={anio} onChange={(e) => setAnio(Number(e.target.value))} className="input !py-1">
              {anios.map((a) => (
                <option key={a} value={a}>{a}</option>
              ))}
            </select>
          </div>
        </div>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {visibles.map((p) => (
            <div key={p.periodoId} className="flex flex-col justify-between rounded-lg border border-soft p-4">
              <div>
                <p className="font-semibold">{p.periodoCodigo}</p>
                <p className="text-sm text-ink-secondary">{p.periodoDescripcion ?? '—'}</p>
                {p.yaDescargado && <span className="badge tint-success">Ya descargado</span>}
              </div>
              <div className="mt-3 flex gap-2">
                <button onClick={() => descargar(p.periodoId, p.periodoCodigo)} disabled={ocupado === p.periodoId} className="btn-primary flex-1">
                  <Download size={16} /> PDF
                </button>
                <button onClick={() => enviarEmail(p.periodoId, p.periodoCodigo)} disabled={ocupado === p.periodoId} className="btn-secondary" title="Enviar por email">
                  <Mail size={16} />
                </button>
              </div>
            </div>
          ))}
        </div>
        {visibles.length === 0 && <p className="text-sm text-ink-secondary">No hay períodos disponibles para {anio}.</p>}
      </div>

      <div className="card">
        <h2 className="mb-3 font-semibold">Historial de descargas</h2>
        {historial.length > 0 ? (
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="border-b text-ink-secondary">
                <th className="py-2">Período</th>
                <th className="py-2">Fecha</th>
                <th className="py-2">Origen</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-soft">
              {historial.map((d) => (
                <tr key={d.id}>
                  <td className="py-2 font-medium">{d.periodoCodigo}</td>
                  <td className="py-2">{formatFechaHora(d.fechaHora)}</td>
                  <td className="py-2">{d.origen === 'Web' ? 'Descarga en el portal' : 'Enviado por email'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <p className="text-sm text-ink-secondary">Todavía no descargaste ningún recibo.</p>
        )}
      </div>
    </div>
  );
}