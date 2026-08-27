import { useEffect, useState } from 'react';
import { Pencil, Plus, Power, X } from 'lucide-react';
import { anunciosApi, adminApi } from '../../api';
import type { AnuncioDto, AreaDto } from '../../types';
import { formatFecha } from '../../utils';

const PRIORIDADES = [
  { valor: 'Normal', nombre: 'Normal' },
  { valor: 'Urgente', nombre: 'Urgente' },
];

const TIPOS = [
  { valor: 'Informativo', nombre: 'Informativo' },
  { valor: 'Institucional', nombre: 'Institucional' },
  { valor: 'Comunicado', nombre: 'Comunicado' },
];

const ALCANCES = [
  { valor: 'Todos', nombre: 'Todos' },
  { valor: 'Area', nombre: 'Por área' },
  { valor: 'Rol', nombre: 'Por rol' },
];

const ROLES = ['Empleado', 'Responsable', 'Rrhh', 'Administrador', 'Direccion', 'HomeOffice'];

export default function AdminAnunciosPage() {
  const [anuncios, setAnuncios] = useState<AnuncioDto[]>([]);
  const [areas, setAreas] = useState<AreaDto[]>([]);
  const [editando, setEditando] = useState<AnuncioDto | null>(null);
  const [creando, setCreando] = useState(false);
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const cargar = () => anunciosApi.admin().then(setAnuncios).catch(() => {});

  useEffect(() => {
    cargar();
    adminApi.areas().then(setAreas).catch(() => {});
  }, []);

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Anuncios</h1>
          <p className="text-sm text-ink-secondary">Gestioná comunicados institucionales. Alcance: todos, por área o por rol.</p>
        </div>
        <button onClick={() => setCreando(true)} className="btn-primary">
          <Plus size={16} /> Nuevo anuncio
        </button>
      </div>

      {mensaje && <p className="rounded-lg tint-success px-3 py-2 text-sm">{mensaje}</p>}
      {error && <p className="rounded-lg tint-danger px-3 py-2 text-sm">{error}</p>}

      <div className="space-y-3">
        {anuncios.map((a) => (
          <div key={a.id} className={`card ${a.activo ? '' : 'opacity-50'}`}>
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-center gap-2">
                  <p className="font-semibold">{a.titulo}</p>
                  {a.prioridad === 'Urgente' && <span className="badge tint-danger">Urgente</span>}
                  <span className={`badge ${a.activo ? 'tint-success' : 'tint-warning'}`}>{a.activo ? 'Activo' : 'Inactivo'}</span>
                  <span className="badge bg-surface-soft text-ink-secondary">{a.tipo}</span>
                  <span className="badge bg-surface-soft text-ink-secondary">{a.alcance}</span>
                </div>
                <p className="mt-1 whitespace-pre-line text-sm text-ink-primary">{a.cuerpo}</p>
                <p className="mt-2 text-xs text-ink-muted">
                  Creado {formatFecha(a.fechaCreacion)}
                  {a.fechaDesde && ` · vigente ${formatFecha(a.fechaDesde)}`}
                  {a.fechaHasta && ` al ${formatFecha(a.fechaHasta)}`}
                </p>
              </div>
              <div className="flex gap-1">
                <button onClick={() => setEditando(a)} className="btn-secondary !px-2 !py-1"><Pencil size={14} /></button>
                <button
                  onClick={async () => {
                    await anunciosApi.setActivo(a.id, !a.activo);
                    cargar();
                  }}
                  className="btn-secondary !px-2 !py-1"
                  title={a.activo ? 'Desactivar' : 'Activar'}
                >
                  <Power size={14} />
                </button>
              </div>
            </div>
          </div>
        ))}
        {anuncios.length === 0 && <p className="text-sm text-ink-secondary">No hay anuncios cargados.</p>}
      </div>

      {(creando || editando) && (
        <AnuncioModal
          anuncio={editando}
          areas={areas}
          onClose={() => { setCreando(false); setEditando(null); }}
          onOk={(m) => { setMensaje(m); setCreando(false); setEditando(null); cargar(); }}
          onError={setError}
        />
      )}
    </div>
  );
}

function AnuncioModal({
  anuncio,
  areas,
  onClose,
  onOk,
  onError,
}: {
  anuncio: AnuncioDto | null;
  areas: AreaDto[];
  onClose: () => void;
  onOk: (m: string) => void;
  onError: (e: string) => void;
}) {
  const [titulo, setTitulo] = useState(anuncio?.titulo ?? '');
  const [cuerpo, setCuerpo] = useState(anuncio?.cuerpo ?? '');
  const [prioridad, setPrioridad] = useState(anuncio?.prioridad ?? 'Normal');
  const [tipo, setTipo] = useState(anuncio?.tipo ?? 'Informativo');
  const [fechaDesde, setFechaDesde] = useState(anuncio?.fechaDesde?.slice(0, 10) ?? '');
  const [fechaHasta, setFechaHasta] = useState(anuncio?.fechaHasta?.slice(0, 10) ?? '');
  const [alcance, setAlcance] = useState(anuncio?.alcance ?? 'Todos');
  const [areaId, setAreaId] = useState<string>('');
  const [rol, setRol] = useState<string>('');
  const [guardando, setGuardando] = useState(false);

  const guardar = async () => {
    if (!titulo.trim() || !cuerpo.trim()) {
      onError('Título y cuerpo son obligatorios.');
      return;
    }
    setGuardando(true);
    try {
      const payload: any = {
        titulo: titulo.trim(),
        cuerpo,
        prioridad,
        tipo,
        fechaDesde: fechaDesde || null,
        fechaHasta: fechaHasta || null,
        alcance,
        areaId: alcance === 'Area' ? areaId || null : null,
        rol: alcance === 'Rol' ? rol || null : null,
      };
      if (anuncio) {
        await anunciosApi.actualizar(anuncio.id, payload);
        onOk('Anuncio actualizado.');
      } else {
        await anunciosApi.crear(payload);
        onOk('Anuncio creado.');
      }
    } catch (e: any) {
      onError(e.response?.data?.error ?? 'No se pudo guardar.');
    } finally {
      setGuardando(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4" onClick={onClose}>
      <div className="card max-h-[90vh] w-full max-w-lg overflow-y-auto" onClick={(e) => e.stopPropagation()}>
        <div className="mb-3 flex items-center justify-between">
          <h2 className="text-lg font-bold">{anuncio ? 'Editar anuncio' : 'Nuevo anuncio'}</h2>
          <button onClick={onClose}><X size={18} /></button>
        </div>
        <div className="space-y-3">
          <div>
            <label className="label">Título</label>
            <input value={titulo} onChange={(e) => setTitulo(e.target.value)} maxLength={200} className="input" placeholder="Ej: Cierre administrativo" />
          </div>
          <div>
            <label className="label">Cuerpo</label>
            <textarea value={cuerpo} onChange={(e) => setCuerpo(e.target.value)} rows={4} className="input resize-y" placeholder="Contenido del anuncio..." />
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            <div>
              <label className="label">Prioridad</label>
              <select value={prioridad} onChange={(e) => setPrioridad(e.target.value)} className="input">
                {PRIORIDADES.map((p) => <option key={p.valor} value={p.valor}>{p.nombre}</option>)}
              </select>
            </div>
            <div>
              <label className="label">Tipo</label>
              <select value={tipo} onChange={(e) => setTipo(e.target.value)} className="input">
                {TIPOS.map((t) => <option key={t.valor} value={t.valor}>{t.nombre}</option>)}
              </select>
            </div>
            <div>
              <label className="label">Vigente desde (opcional)</label>
              <input type="date" value={fechaDesde} onChange={(e) => setFechaDesde(e.target.value)} className="input" />
            </div>
            <div>
              <label className="label">Vigente hasta (opcional)</label>
              <input type="date" value={fechaHasta} onChange={(e) => setFechaHasta(e.target.value)} className="input" />
            </div>
          </div>
          <div>
            <label className="label">Alcance</label>
            <select value={alcance} onChange={(e) => setAlcance(e.target.value)} className="input">
              {ALCANCES.map((a) => <option key={a.valor} value={a.valor}>{a.nombre}</option>)}
            </select>
          </div>
          {alcance === 'Area' && (
            <div>
              <label className="label">Área</label>
              <select value={areaId} onChange={(e) => setAreaId(e.target.value)} className="input">
                <option value="">Seleccionar área</option>
                {areas.map((a) => <option key={a.id} value={a.id}>{a.nombre}</option>)}
              </select>
            </div>
          )}
          {alcance === 'Rol' && (
            <div>
              <label className="label">Rol</label>
              <select value={rol} onChange={(e) => setRol(e.target.value)} className="input">
                <option value="">Seleccionar rol</option>
                {ROLES.map((r) => <option key={r} value={r}>{r}</option>)}
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
