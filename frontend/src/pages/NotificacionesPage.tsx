import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Bell } from 'lucide-react';
import { notificacionesApi } from '../api';
import type { NotificacionDto } from '../types';
import { formatFechaHora } from '../utils';

export default function NotificacionesPage() {
  const [notificaciones, setNotificaciones] = useState<NotificacionDto[]>([]);

  const cargar = () => notificacionesApi.mias().then(setNotificaciones).catch(() => {});

  useEffect(() => { cargar(); }, []);

  const marcarLeida = async (id: string) => {
    await notificacionesApi.marcarLeida(id);
    cargar();
  };

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Notificaciones</h1>
        <p className="text-sm text-ink-secondary">Avisos sobre licencias, anuncios y más.</p>
      </div>

      {notificaciones.length === 0 ? (
        <div className="card flex items-center gap-3 text-ink-secondary">
          <Bell size={20} /> No tenés notificaciones.
        </div>
      ) : (
        <div className="space-y-2">
          {notificaciones.map((n) => (
            <div key={n.id} className={`card !p-4 ${n.leida ? 'opacity-70' : 'border-accent/40'}`}>
              <div className="flex items-start justify-between gap-3">
                <div>
                  <p className="font-medium">{n.titulo}</p>
                  <p
                    className="mt-1 text-sm text-ink-secondary"
                    dangerouslySetInnerHTML={{ __html: n.cuerpo }}
                  />
                  <p className="mt-1 text-xs text-ink-muted">{formatFechaHora(n.fechaHora)}</p>
                </div>
                <div className="flex shrink-0 flex-col items-end gap-2">
                  {!n.leida && <span className="h-2 w-2 rounded-full bg-accent" />}
                  {n.link && (
                    <Link to={n.link} className="text-xs text-accent-text hover:underline">
                      Ir →
                    </Link>
                  )}
                  {!n.leida && (
                    <button onClick={() => marcarLeida(n.id)} className="btn-secondary !px-2 !py-1 text-xs">
                      Marcar leída
                    </button>
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}