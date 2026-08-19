import { useEffect, useState } from 'react';
import { Pencil, Plus, Power, X } from 'lucide-react';
import { adminApi } from '../../api';
import type { TipoLicenciaDto } from '../../types';

const ROLES_APROBADOR = ['ResponsableDirecto', 'Responsable', 'Rrhh', 'Direccion', 'Administrador'];

export default function AdminTiposLicenciaPage() {
  const [tipos, setTipos] = useState<TipoLicenciaDto[]>([]);
  const [editando, setEditando] = useState<TipoLicenciaDto | null>(null);
  const [creando, setCreando] = useState(false);
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const cargar = () => adminApi.tiposLicencia().then(setTipos).catch(() => {});

  useEffect(() => { cargar(); }, []);

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Tipos de licencia</h1>
          <p className="text-sm text-ink-secondary">Definición de límites y niveles de aprobación.</p>
        </div>
        <button onClick={() => setCreando(true)} className="btn-primary">
          <Plus size={16} /> Nuevo
        </button>
      </div>

      {mensaje && <p className="rounded-lg tint-success px-3 py-2 text-sm">{mensaje}</p>}
      {error && <p className="rounded-lg tint-danger px-3 py-2 text-sm">{error}</p>}

      <div className="space-y-3">
        {tipos.map((t) => (
          <div key={t.id} className={`card ${t.activo ? '' : 'opacity-50'}`}>
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <p className="font-semibold">{t.nombre}</p>
                <p className="text-sm text-ink-secondary">
                  Límite mensual: {t.limiteMensual ?? '—'} · Anual: {t.limiteAnual ?? '—'}
                  {t.requiereAdjunto ? ' · Requiere adjunto' : ''}
                </p>
              </div>
              <div className="flex gap-1">
                <button onClick={() => setEditando(t)} className="btn-secondary !px-2 !py-1"><Pencil size={14} /></button>
                <button
                  onClick={async () => {
                    await adminApi.setActivoTipo(t.id, !t.activo);
                    cargar();
                  }}
                  className={`btn-secondary !px-2 !py-1 ${t.activo ? '' : 'text-success'}`}
                >
                  <Power size={14} />
                </button>
              </div>
            </div>
            {t.niveles.length > 0 && (
              <div className="mt-3 flex flex-wrap items-center gap-2">
                {t.niveles.map((n) => (
                  <span key={n.id} className="badge bg-surface-soft text-ink-primary">
                    {n.orden}. {n.rolRequerido}
                    <button
                      onClick={async () => {
                        await adminApi.quitarNivel(n.id);
                        cargar();
                      }}
                      className="ml-2 text-ink-muted hover:text-danger"
                    >
                      <X size={12} />
                    </button>
                  </span>
                ))}
              </div>
            )}
          </div>
        ))}
      </div>

      {(creando || editando) && (
        <TipoModal
          tipo={editando}
          onClose={() => { setCreando(false); setEditando(null); }}
          onOk={async (m) => {
            setMensaje(m);
            setCreando(false);
            setEditando(null);
            cargar();
          }}
          onError={setError}
        />
      )}
    </div>
  );
}

function TipoModal({
  tipo,
  onClose,
  onOk,
  onError
}: {
  tipo: TipoLicenciaDto | null;
  onClose: () => void;
  onOk: (m: string) => void;
  onError: (e: string) => void;
}) {
  const [nombre, setNombre] = useState(tipo?.nombre ?? '');
  const [limiteMensual, setLimiteMensual] = useState<string>(tipo?.limiteMensual?.toString() ?? '');
  const [limiteAnual, setLimiteAnual] = useState<string>(tipo?.limiteAnual?.toString() ?? '');
  const [descripcion, setDescripcion] = useState(tipo?.descripcion ?? '');
  const [requiereAdjunto, setRequiereAdjunto] = useState(tipo?.requiereAdjunto ?? false);
  const [nivel, setNivel] = useState(ROLES_APROBADOR[0]);
  const [guardando, setGuardando] = useState(false);

  const guardar = async () => {
    setGuardando(true);
    try {
      if (tipo) {
        await adminApi.actualizarTipoLicencia(tipo.id, {
          nombre,
          limiteMensual: limiteMensual ? Number(limiteMensual) : null,
          limiteAnual: limiteAnual ? Number(limiteAnual) : null,
          descripcion: descripcion || undefined,
          requiereAdjunto
        });
        onOk('Tipo de licencia actualizado.');
      } else {
        const t = await adminApi.crearTipoLicencia({
          nombre,
          limiteMensual: limiteMensual ? Number(limiteMensual) : null,
          limiteAnual: limiteAnual ? Number(limiteAnual) : null,
          descripcion: descripcion || undefined,
          requiereAdjunto,
          niveles: []
        });
        if (nivel) await adminApi.agregarNivel(t.id, nivel);
        onOk('Tipo de licencia creado.');
      }
    } catch (e: any) {
      onError(e.response?.data?.error ?? 'No se pudo guardar.');
    } finally {
      setGuardando(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4" onClick={onClose}>
      <div className="card w-full max-w-md" onClick={(e) => e.stopPropagation()}>
        <div className="mb-3 flex items-center justify-between">
          <h2 className="text-lg font-bold">{tipo ? 'Editar tipo' : 'Nuevo tipo de licencia'}</h2>
          <button onClick={onClose}><X size={18} /></button>
        </div>
        <div className="space-y-3">
          <div>
            <label className="label">Nombre</label>
            <input value={nombre} onChange={(e) => setNombre(e.target.value)} className="input" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="label">Límite mensual (días)</label>
              <input type="number" value={limiteMensual} onChange={(e) => setLimiteMensual(e.target.value)} className="input" />
            </div>
            <div>
              <label className="label">Límite anual (días)</label>
              <input type="number" value={limiteAnual} onChange={(e) => setLimiteAnual(e.target.value)} className="input" />
            </div>
          </div>
          <div>
            <label className="label">Descripción</label>
            <input value={descripcion} onChange={(e) => setDescripcion(e.target.value)} className="input" />
          </div>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={requiereAdjunto} onChange={(e) => setRequiereAdjunto(e.target.checked)} />
            Requiere adjuntar certificado
          </label>
          {!tipo && (
            <div>
              <label className="label">Primer nivel de aprobación</label>
              <select value={nivel} onChange={(e) => setNivel(e.target.value)} className="input">
                {ROLES_APROBADOR.map((r) => (
                  <option key={r} value={r}>{r}</option>
                ))}
              </select>
            </div>
          )}
          <button onClick={guardar} disabled={guardando} className="btn-primary w-full">
            {guardando ? 'Guardando...' : 'Guardar'}
          </button>
        </div>
      </div>
    </div>
  );
}