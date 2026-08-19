import { useEffect, useState } from 'react';
import { Check, X } from 'lucide-react';
import { licenciasApi } from '../api';
import type { SolicitudDetalleDto, SolicitudLicenciaDto } from '../types';
import { formatFecha } from '../utils';

export default function PendientesPage() {
  const [pendientes, setPendientes] = useState<SolicitudLicenciaDto[]>([]);
  const [detalle, setDetalle] = useState<SolicitudDetalleDto | null>(null);
  const [comentario, setComentario] = useState('');
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const cargar = () => licenciasApi.pendientes().then(setPendientes).catch(() => {});

  useEffect(() => { cargar(); }, []);

  const decidir = async (aprobado: boolean) => {
    if (!detalle) return;
    try {
      await licenciasApi.decidir(detalle.solicitud.id, aprobado, comentario || undefined);
      setMensaje(aprobado ? 'Licencia aprobada.' : 'Licencia rechazada.');
      setDetalle(null);
      setComentario('');
      cargar();
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo registrar la decisión.');
    }
  };

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Aprobaciones pendientes</h1>
        <p className="text-sm text-ink-secondary">Solicitudes que esperan tu decisión.</p>
      </div>

      {mensaje && <p className="rounded-lg tint-success px-3 py-2 text-sm">{mensaje}</p>}
      {error && <p className="rounded-lg tint-danger px-3 py-2 text-sm">{error}</p>}

      {pendientes.length === 0 ? (
        <div className="card text-center text-ink-secondary">No tenés solicitudes pendientes de aprobación.</div>
      ) : (
        <div className="space-y-3">
          {pendientes.map((s) => (
            <div key={s.id} className="card flex flex-wrap items-center justify-between gap-3">
              <div>
                <p className="font-medium">
                  {s.empleadoNombre} <span className="text-ink-muted">· {s.tipoLicenciaNombre}</span>
                </p>
                <p className="text-sm text-ink-secondary">
                  {formatFecha(s.fechaInicio)} → {formatFecha(s.fechaFin)} · {s.dias} días
                </p>
                {s.motivo && <p className="mt-1 text-sm text-ink-secondary">{s.motivo}</p>}
              </div>
              <button onClick={() => { setDetalle(null); licenciasApi.detalle(s.id).then(setDetalle); }} className="btn-secondary">
                Ver y decidir
              </button>
            </div>
          ))}
        </div>
      )}

      {detalle && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4" onClick={() => setDetalle(null)}>
          <div className="card max-h-[90vh] w-full max-w-lg overflow-y-auto" onClick={(e) => e.stopPropagation()}>
            <h2 className="mb-2 text-lg font-bold">{detalle.solicitud.empleadoNombre}</h2>
            <p className="text-sm text-ink-secondary">
              {detalle.solicitud.tipoLicenciaNombre} · {formatFecha(detalle.solicitud.fechaInicio)} →{' '}
              {formatFecha(detalle.solicitud.fechaFin)} · {detalle.solicitud.dias} días
            </p>
            {detalle.solicitud.motivo && <p className="mt-2 text-sm text-ink-secondary">{detalle.solicitud.motivo}</p>}
            {detalle.solicitud.adjuntoId && (
              <p className="mt-1 text-sm text-accent-text">Adjunta un archivo (certificado).</p>
            )}

            <div className="mt-4 space-y-2">
              {detalle.niveles.map((n) => (
                <div key={n.id} className="flex items-center justify-between rounded-lg bg-surface-soft px-3 py-2 text-sm">
                  <span>
                    Nivel {n.orden}: {n.rolRequerido}
                    {n.aprobadorNombre ? ` (${n.aprobadorNombre})` : ''}
                  </span>
                  <span className={`badge ${n.aprobado ? 'tint-success' : n.aprobadorId ? 'tint-danger' : 'tint-warning'}`}>
                    {n.aprobado ? 'Aprobado' : n.aprobadorId ? 'Rechazado' : 'Pendiente'}
                  </span>
                </div>
              ))}
            </div>

            {detalle.puedeAprobar ? (
              <div className="mt-4 space-y-3">
                <textarea
                  value={comentario}
                  onChange={(e) => setComentario(e.target.value)}
                  className="input"
                  rows={2}
                  placeholder="Comentario (opcional)"
                />
                <div className="flex gap-2">
                  <button onClick={() => decidir(true)} className="btn-primary flex-1">
                    <Check size={16} /> Aprobar
                  </button>
                  <button onClick={() => decidir(false)} className="btn-danger flex-1">
                    <X size={16} /> Rechazar
                  </button>
                </div>
              </div>
            ) : (
              <p className="mt-4 text-sm text-amber-700">{detalle.razonBloqueo ?? 'No podés decidir sobre esta solicitud.'}</p>
            )}
          </div>
        </div>
      )}
    </div>
  );
}