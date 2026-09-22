import { useEffect, useState } from 'react';
import { BarChart3, Eye, Loader2, Lock, Pencil, PenTool, Plus, Trash2 } from 'lucide-react';
import { reportesApi } from '../api';
import type { PermisoDto, ReporteCompletoDto, ReporteResumenDto } from '../types';
import ReportesBuilder from './ReportesBuilder';
import ReporteCanvas from './ReporteCanvas';
import ReporteViewer from './ReporteViewer';

const ROLES_DISPONIBLES = ['Empleado', 'Responsable', 'Rrhh', 'Administrador', 'Direccion', 'HomeOffice'];

export default function ReportesAdmin() {
  const [reportes, setReportes] = useState<ReporteResumenDto[]>([]);
  const [vista, setVista] = useState<{ tipo: 'builder' | 'viewer' | 'canvas'; reporte: ReporteCompletoDto | null } | null>(null);
  const [permisosDe, setPermisosDe] = useState<ReporteCompletoDto | null>(null);
  const [nuevoEmail, setNuevoEmail] = useState('');
  const [cargando, setCargando] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const cargar = () => reportesApi.todos().then(setReportes).catch(() => {});
  useEffect(() => { cargar(); }, []);

  if (vista) {
    return (
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold">
            {vista.tipo === 'builder' ? 'Diseñador de datos' : vista.tipo === 'canvas' ? 'Diseño de apariencia' : 'Vista previa'} — {vista.reporte?.nombre ?? 'Nuevo reporte'}
          </h2>
          {vista.tipo === 'builder' && vista.reporte && (
            <button
              onClick={async () => {
                setVista({ tipo: 'viewer', reporte: await reportesApi.porId(vista.reporte!.id) });
              }}
              className="btn-secondary flex items-center gap-2 text-sm"
            ><Eye size={14} /> Probar</button>
          )}
        </div>
        {vista.tipo === 'canvas' ? (
          vista.reporte && (
            <ReporteCanvas reporte={vista.reporte} onCerrar={() => { setVista(null); cargar(); }} />
          )
        ) : vista.tipo === 'builder' ? (
          <ReportesBuilder
            reporte={vista.reporte}
            onGuardado={async (idRecien) => {
              setVista({ tipo: 'builder', reporte: await reportesApi.porId(idRecien) });
            }}
            onCerrar={() => { setVista(null); cargar(); }}
          />
        ) : (
          <div className="space-y-3">
            <button onClick={() => setVista({ tipo: 'builder', reporte: vista.reporte })} className="btn-secondary text-sm">
              ← Volver al diseñador
            </button>
            {vista.reporte && <ReporteViewer reporte={vista.reporte} />}
          </div>
        )}
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-ink-secondary">
          Reportes visuales construidos con el designer. Los Administradores ven todos; el resto solo los autorizados.
        </p>
        <button onClick={() => setVista({ tipo: 'builder', reporte: null })} className="btn-primary flex items-center gap-2">
          <Plus size={14} /> Nuevo reporte
        </button>
      </div>

      {reportes.length === 0 ? (
        <p className="text-sm text-ink-secondary">No hay reportes todavía.</p>
      ) : (
        <div className="space-y-2">
          {reportes.map((r) => (
            <div key={r.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-soft p-3">
              <div className="min-w-0">
                <p className="flex items-center gap-2 font-medium">
                  <BarChart3 size={15} /> {r.nombre}
                  {!r.activo && <span className="badge tint-warning">inactivo</span>}
                  <span className="badge bg-black/5 text-[10px] text-ink-secondary">{r.creadoPorEmail}</span>
                </p>
                {r.descripcion && <p className="text-xs text-ink-secondary">{r.descripcion}</p>}
              </div>
              <div className="flex items-center gap-1">
                <button
                  onClick={async () => setVista({ tipo: 'viewer', reporte: await reportesApi.porId(r.id) })}
                  className="btn-secondary !px-2 !py-1 text-xs" title="Ver reporte"
                ><Eye size={14} /></button>
                {r.puedeEditar && (
                  <button
                    onClick={async () => setVista({ tipo: 'builder', reporte: await reportesApi.porId(r.id) })}
                    className="btn-secondary !px-2 !py-1" title="Editar"
                  ><Pencil size={14} /></button>
                )}
                {r.puedeEditar && (
                  <button
                    onClick={async () => setVista({ tipo: 'canvas', reporte: await reportesApi.porId(r.id) })}
                    className="btn-secondary !px-2 !py-1" title="Diseño de apariencia (canvas)"
                  ><PenTool size={14} /></button>
                )}
                {r.puedeEditar && (
                  <button
                    onClick={async () => setPermisosDe(await reportesApi.porId(r.id))}
                    className="btn-secondary !px-2 !py-1" title="Permisos"
                  ><Lock size={14} /></button>
                )}
                {r.puedeEditar && (
                  <button
                    onClick={() => {
                      if (!confirm(`¿Eliminar el reporte "${r.nombre}"?`)) return;
                      setCargando(true);
                      reportesApi.eliminar(r.id).then(cargar).catch((e) => setError(e.response?.data?.error)).finally(() => setCargando(false));
                    }}
                    className="btn-danger !px-2 !py-1" title="Eliminar"
                  ><Trash2 size={14} /></button>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {error && <p className="text-sm text-danger">{error}</p>}
      {cargando && <p className="text-sm text-ink-secondary">Trabajando…</p>}

      {permisosDe && (
        <ModalPermisos
          reporte={permisosDe}
          nuevoEmail={nuevoEmail}
          setNuevoEmail={setNuevoEmail}
          onCerrar={() => setPermisosDe(null)}
          onGuardar={async (permisos) => {
            await reportesApi.permisos(permisosDe.id, permisos);
            setPermisosDe(null);
          }}
        />
      )}
    </div>
  );
}

function ModalPermisos({ reporte, nuevoEmail, setNuevoEmail, onCerrar, onGuardar }: {
  reporte: ReporteCompletoDto;
  nuevoEmail: string;
  setNuevoEmail: (v: string) => void;
  onCerrar: () => void;
  onGuardar: (permisos: PermisoDto[]) => Promise<void>;
}) {
  const [permisos, setPermisos] = useState<PermisoDto[]>(reporte.permisos);
  const [guardando, setGuardando] = useState(false);

  const agregarEmail = () => {
    if (!nuevoEmail.trim()) return;
    setPermisos((p) => [...p, { email: nuevoEmail.trim().toLowerCase(), rol: null }]);
    setNuevoEmail('');
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4" onClick={onCerrar}>
      <div className="w-full max-w-lg rounded-xl bg-white p-5 shadow-2xl" onClick={(e) => e.stopPropagation()}>
        <h3 className="mb-1 font-semibold">Quién puede ver "{reporte.nombre}"</h3>
        <p className="mb-3 text-xs text-ink-secondary">El creador y los Administradores siempre pueden verlo.</p>
        <div className="mb-3 space-y-1">
          {permisos.map((p, i) => (
            <div key={i} className="flex items-center justify-between rounded border border-soft px-2 py-1 text-xs">
              <span>{p.email ? p.email : `rol: ${p.rol}`}</span>
              <button onClick={() => setPermisos(permisos.filter((_, j) => j !== i))} className="text-ink-muted hover:text-danger">
                <Trash2 size={12} />
              </button>
            </div>
          ))}
          {permisos.length === 0 && <p className="text-xs text-ink-muted">Solo el creador y Administradores.</p>}
        </div>
        <div className="mb-3 flex gap-2">
          <input
            value={nuevoEmail}
            onChange={(e) => setNuevoEmail(e.target.value)}
            placeholder="correo@iupa.edu.ar"
            className="input flex-1 text-xs"
            onKeyDown={(e) => e.key === 'Enter' && agregarEmail()}
          />
          <button onClick={agregarEmail} className="btn-secondary text-xs">+ correo</button>
        </div>
        <p className="mb-2 text-[10px] uppercase tracking-wide text-ink-muted">Roles autorizados</p>
        <div className="mb-4 flex flex-wrap gap-3">
          {ROLES_DISPONIBLES.map((rol) => (
            <label key={rol} className="flex items-center gap-1 text-xs">
              <input
                type="checkbox"
                checked={permisos.some((p) => p.rol === rol)}
                onChange={(e) => setPermisos((prev) =>
                  e.target.checked ? [...prev, { email: null, rol }] : prev.filter((p) => p.rol !== rol)
                )}
              />
              {rol}
            </label>
          ))}
        </div>
        <div className="flex justify-end gap-2">
          <button onClick={onCerrar} className="btn-secondary">Cancelar</button>
          <button
            onClick={async () => { setGuardando(true); await onGuardar(permisos); setGuardando(false); }}
            className="btn-primary flex items-center gap-2"
          >
            {guardando && <Loader2 size={14} className="animate-spin" />} Guardar permisos
          </button>
        </div>
      </div>
    </div>
  );
}
