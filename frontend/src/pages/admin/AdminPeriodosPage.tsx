import { useEffect, useState } from 'react';
import { Pencil, Plus, Power, RefreshCw, X } from 'lucide-react';
import { adminApi } from '../../api';
import type { PeriodoDto } from '../../types';

export default function AdminPeriodosPage() {
  const [periodos, setPeriodos] = useState<PeriodoDto[]>([]);
  const [editando, setEditando] = useState<PeriodoDto | null>(null);
  const [creando, setCreando] = useState(false);
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [sincronizando, setSincronizando] = useState(false);
  const [anio, setAnio] = useState(new Date().getFullYear());

  const cargar = () => adminApi.periodos().then(setPeriodos).catch(() => {});

  useEffect(() => { cargar(); }, []);

  const sincronizar = async () => {
    setError(null);
    setMensaje(null);
    setSincronizando(true);
    try {
      const r = await adminApi.sincronizarPeriodos();
      setMensaje(
        `Sincronización desde SIU-Mapuche: ${r.importados} importados, ${r.actualizados} actualizados, ` +
        `${r.sinCambios} sin cambios, ${r.conError} con error (${r.totalExternos} liquidaciones en origen).`
      );
      cargar();
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo sincronizar. Revisá la conexión con SIU-Mapuche.');
    } finally {
      setSincronizando(false);
    }
  };

  const anioDe = (p: PeriodoDto): number | null => {
    const m = p.codigo.match(/^(\d{4})-/);
    if (m) return Number(m[1]);
    const d = p.descripcion?.match(/\b(19|20)\d{2}\b/);
    return d ? Number(d[0]) : null;
  };

  const anios = Array.from(
    new Set([new Date().getFullYear(), ...periodos.map((p) => anioDe(p)).filter((a): a is number => a !== null)])
  ).sort((a, b) => b - a);

  const visibles = periodos.filter((p) => anioDe(p) === null || anioDe(p) === anio);

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Períodos</h1>
        <div className="flex gap-2">
          <button onClick={sincronizar} disabled={sincronizando} className="btn-secondary">
            <RefreshCw size={16} className={sincronizando ? 'animate-spin' : ''} />
            {sincronizando ? 'Sincronizando...' : 'Sincronizar con SIU-Mapuche'}
          </button>
          <button onClick={() => setCreando(true)} className="btn-primary">
            <Plus size={16} /> Nuevo
          </button>
        </div>
      </div>

      <div className="card p-0">
        <div className="flex items-center justify-between gap-3 p-4">
          <p className="text-sm text-ink-secondary">
            Períodos de liquidación disponibles para descargar recibos. Podés sincronizarlos automáticamente desde SIU-Mapuche (las liquidaciones cerradas se importan activas).
          </p>
          <div className="flex items-center gap-2">
            <label className="text-sm text-ink-secondary">Año</label>
            <select value={anio} onChange={(e) => setAnio(Number(e.target.value))} className="input !py-1">
              {anios.map((a) => (
                <option key={a} value={a}>{a}</option>
              ))}
            </select>
          </div>
        </div>
      </div>

      {mensaje && <p className="rounded-lg tint-success px-3 py-2 text-sm">{mensaje}</p>}
      {error && <p className="rounded-lg tint-danger px-3 py-2 text-sm">{error}</p>}

      <div className="space-y-3">
        {visibles.map((p) => (
          <div key={p.id} className={`card ${p.activo ? '' : 'opacity-50'}`}>
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div className="flex items-center gap-3">
                <span className="badge bg-surface-soft text-ink-primary">{p.codigo}</span>
                <div>
                  <p className="font-semibold">{p.descripcion || 'Sin descripción'}</p>
                  <p className="text-sm text-ink-secondary">NroLiq: {p.nroLiq ?? '—'}</p>
                </div>
              </div>
              <div className="flex gap-1">
                <span className={`badge ${p.activo ? 'tint-success' : 'tint-warning'}`}>
                  {p.activo ? 'Activo' : 'Inactivo'}
                </span>
                <button onClick={() => setEditando(p)} className="btn-secondary !px-2 !py-1"><Pencil size={14} /></button>
                <button
                  onClick={async () => {
                    await adminApi.actualizarPeriodo(p.id, {
                      descripcion: p.descripcion ?? undefined,
                      activo: !p.activo,
                      nroLiq: p.nroLiq
                    });
                    cargar();
                  }}
                  className={`btn-secondary !px-2 !py-1 ${p.activo ? '' : 'text-success'}`}
                  title={p.activo ? 'Desactivar' : 'Activar'}
                >
                  <Power size={14} />
                </button>
              </div>
            </div>
          </div>
        ))}
        {periodos.length === 0 && <p className="text-sm text-ink-secondary">No hay períodos cargados.</p>}
      </div>

      {(creando || editando) && (
        <PeriodoModal
          periodo={editando}
          onClose={() => { setCreando(false); setEditando(null); }}
          onOk={(m) => {
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

function PeriodoModal({
  periodo,
  onClose,
  onOk,
  onError
}: {
  periodo: PeriodoDto | null;
  onClose: () => void;
  onOk: (m: string) => void;
  onError: (e: string) => void;
}) {
  const [codigo, setCodigo] = useState(periodo?.codigo ?? '');
  const [descripcion, setDescripcion] = useState(periodo?.descripcion ?? '');
  const [nroLiq, setNroLiq] = useState(periodo?.nroLiq?.toString() ?? '');
  const [activo, setActivo] = useState(periodo?.activo ?? true);
  const [guardando, setGuardando] = useState(false);

  const guardar = async () => {
    setGuardando(true);
    try {
      if (periodo) {
        await adminApi.actualizarPeriodo(periodo.id, {
          descripcion: descripcion || undefined,
          activo,
          nroLiq: nroLiq ? Number(nroLiq) : null
        });
        onOk('Período actualizado.');
      } else {
        await adminApi.crearPeriodo({
          codigo,
          descripcion: descripcion || undefined,
          nroLiq: nroLiq ? Number(nroLiq) : null
        });
        onOk('Período creado.');
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
          <h2 className="text-lg font-bold">{periodo ? 'Editar período' : 'Nuevo período'}</h2>
          <button onClick={onClose}><X size={18} /></button>
        </div>
        <div className="space-y-3">
          {!periodo && (
            <div>
              <label className="label">Código</label>
              <input value={codigo} onChange={(e) => setCodigo(e.target.value)} placeholder="ej. 2026-06" className="input" />
            </div>
          )}
          <div>
            <label className="label">Descripción</label>
            <input value={descripcion} onChange={(e) => setDescripcion(e.target.value)} placeholder="ej. Junio 2026" className="input" />
          </div>
          <div>
            <label className="label">NroLiq (número de liquidación en el sistema)</label>
            <input type="number" value={nroLiq} onChange={(e) => setNroLiq(e.target.value)} className="input" />
          </div>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={activo} onChange={(e) => setActivo(e.target.checked)} />
            Activo (visible en Recibos)
          </label>
          <button onClick={guardar} disabled={guardando} className="btn-primary w-full">
            {guardando ? 'Guardando...' : 'Guardar'}
          </button>
        </div>
      </div>
    </div>
  );
}
