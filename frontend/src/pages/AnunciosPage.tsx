import { useEffect, useState } from 'react';
import { CheckCheck } from 'lucide-react';
import { anunciosApi } from '../api';
import type { AnuncioDto } from '../types';
import { formatFecha } from '../utils';

export default function AnunciosPage() {
  const [anuncios, setAnuncios] = useState<AnuncioDto[]>([]);

  const cargar = () => anunciosApi.feed().then(setAnuncios).catch(() => {});

  useEffect(() => { cargar(); }, []);

  const marcarLeido = async (id: string) => {
    await anunciosApi.marcarLeido(id);
    cargar();
  };

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Anuncios</h1>
        <p className="text-sm text-ink-secondary">Novedades institucionales y comunicados.</p>
      </div>

      {anuncios.length === 0 && <div className="card text-center text-ink-secondary">No hay anuncios.</div>}

      <div className="space-y-4">
        {anuncios.map((a) => (
          <article key={a.id} className={`card ${a.prioridad === 'Urgente' ? 'border border-soft tint-warning' : ''}`}>
            <div className="flex items-start justify-between gap-3">
              <div>
                <div className="flex flex-wrap items-center gap-2">
                  <h2 className="font-semibold">{a.titulo}</h2>
                  {a.prioridad === 'Urgente' && <span className="badge tint-danger">Urgente</span>}
                  {a.tipo === 'Comunicado' && <span className="badge bg-accent/15 text-accent-text">Comunicado</span>}
                  {!a.leido && <span className="badge bg-surface-alt text-ink-secondary">Nuevo</span>}
                </div>
                <p className="text-xs text-ink-muted">
                  {formatFecha(a.fechaCreacion)} {a.fechaDesde && a.fechaHasta ? `· vigente ${formatFecha(a.fechaDesde)} al ${formatFecha(a.fechaHasta)}` : ''}
                </p>
              </div>
              {!a.leido && (
                <button onClick={() => marcarLeido(a.id)} className="btn-secondary !px-2 !py-1 text-xs" title="Marcar como leído">
                  <CheckCheck size={14} /> Leído
                </button>
              )}
            </div>
            <p className="mt-2 whitespace-pre-line text-sm text-ink-primary">{a.cuerpo}</p>
          </article>
        ))}
      </div>
    </div>
  );
}