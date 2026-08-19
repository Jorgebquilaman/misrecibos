import { useEffect, useState } from 'react';
import { ChevronDown, ChevronLeft, ChevronRight, ChevronUp, FileSpreadsheet, FileText, Pencil, Plus, Power, X } from 'lucide-react';
import { adminApi } from '../../api';
import { useAuthStore } from '../../store/authStore';
import type { AreaDto, EmpleadoDto } from '../../types';
import { descargarBlob, ETIQUETA_ROL } from '../../utils';

const ROLES = ['Empleado', 'Responsable', 'Rrhh', 'Administrador', 'Direccion', 'HomeOffice'];
const TAMANOS = [10, 20, 50, 100];
const COLUMNAS: { clave: string; etiqueta: string }[] = [
  { clave: 'legajo', etiqueta: 'Legajo' },
  { clave: 'nombre', etiqueta: 'Nombre' },
  { clave: 'correo', etiqueta: 'Correo' },
  { clave: 'area', etiqueta: 'Área' }
];

export default function AdminEmpleadosPage() {
  const usuario = useAuthStore((s) => s.usuario);
  const [empleados, setEmpleados] = useState<EmpleadoDto[]>([]);
  const [areas, setAreas] = useState<AreaDto[]>([]);
  const [texto, setTexto] = useState('');
  const [pagina, setPagina] = useState(1);
  const [tamano, setTamano] = useState(20);
  const [total, setTotal] = useState(0);
  const [orden, setOrden] = useState('nombre');
  const [descendente, setDescendente] = useState(false);
  const [editando, setEditando] = useState<EmpleadoDto | null>(null);
  const [creando, setCreando] = useState(false);
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const esAdmin = usuario?.roles.includes('Administrador');

  const cargar = () => {
    adminApi.empleados({ texto: texto || undefined, pagina, tamano, orden, descendente })
      .then((r) => {
        setEmpleados(r.items);
        setTotal(r.total);
      })
      .catch(() => {});
    adminApi.areas().then(setAreas).catch(() => {});
  };

  useEffect(() => {
    setPagina(1);
  }, [texto, tamano, orden, descendente]);

  useEffect(() => { cargar(); }, [texto, pagina, tamano, orden, descendente]);

  const totalPaginas = Math.max(1, Math.ceil(total / tamano));
  const desde = total === 0 ? 0 : (pagina - 1) * tamano + 1;
  const hasta = Math.min(pagina * tamano, total);

  const cambiarOrden = (clave: string) => {
    if (orden === clave) {
      setDescendente((d) => !d);
    } else {
      setOrden(clave);
      setDescendente(false);
    }
  };

  const exportar = async (formato: 'xlsx' | 'pdf') => {
    setError(null);
    try {
      const blob = await adminApi.exportarEmpleados(formato, { texto: texto || undefined, orden, descendente });
      const fecha = new Date().toISOString().slice(0, 10);
      descargarBlob(blob, `Empleados_${fecha}.${formato}`);
    } catch (e: any) {
      setError(e.response?.data?.error ?? `No se pudo generar el archivo ${formato.toUpperCase()}.`);
    }
  };

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold">Empleados</h1>
          <p className="text-sm text-ink-secondary">Gestión de empleados, roles y áreas.</p>
        </div>
        <div className="flex gap-2">
          <button onClick={() => exportar('xlsx')} className="btn-secondary" title="Descargar en Excel">
            <FileSpreadsheet size={16} /> XLSX
          </button>
          <button onClick={() => exportar('pdf')} className="btn-secondary" title="Descargar en PDF">
            <FileText size={16} /> PDF
          </button>
          <input value={texto} onChange={(e) => setTexto(e.target.value)} className="input !w-48" placeholder="Buscar..." />
          <button onClick={() => setCreando(true)} className="btn-primary">
            <Plus size={16} /> Nuevo
          </button>
        </div>
      </div>

      {mensaje && <p className="rounded-lg tint-success px-3 py-2 text-sm">{mensaje}</p>}
      {error && <p className="rounded-lg tint-danger px-3 py-2 text-sm">{error}</p>}

      <div className="card overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b text-ink-secondary">
              {COLUMNAS.map((c) => (
                <th key={c.clave} className="py-2">
                  <button onClick={() => cambiarOrden(c.clave)} className="inline-flex items-center gap-1 font-medium">
                    {c.etiqueta}
                    {orden === c.clave ? (descendente ? <ChevronDown size={14} /> : <ChevronUp size={14} />) : null}
                  </button>
                </th>
              ))}
              <th className="py-2">Roles</th>
              <th className="py-2"></th>
            </tr>
          </thead>
          <tbody className="divide-y divide-soft">
            {empleados.map((e) => (
              <tr key={e.id} className={e.activo ? '' : 'opacity-50'}>
                <td className="py-2">{e.legajo}</td>
                <td className="py-2 font-medium">{e.apellido}, {e.nombre}</td>
                <td className="py-2">{e.correo}</td>
                <td className="py-2">{e.areaNombre ?? '—'}</td>
                <td className="py-2">
                  <div className="flex flex-wrap gap-1">
                    {e.roles.map((r) => (
                      <span key={r} className="badge bg-accent/15 text-accent-text">{ETIQUETA_ROL[r] ?? r}</span>
                    ))}
                  </div>
                </td>
                <td className="py-2">
                  <div className="flex justify-end gap-1">
                    <button onClick={() => setEditando(e)} className="btn-secondary !px-2 !py-1" title="Editar">
                      <Pencil size={14} />
                    </button>
                    {esAdmin && (
                      <button
                        onClick={async () => {
                          await adminApi.setActivoEmpleado(e.id, !e.activo);
                          cargar();
                        }}
                        className={`btn-secondary !px-2 !py-1 ${e.activo ? '' : 'text-success'}`}
                        title={e.activo ? 'Desactivar' : 'Activar'}
                      >
                        <Power size={14} />
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-soft px-1 py-3 text-sm">
          <div className="flex items-center gap-2">
            <span className="text-ink-secondary">Mostrando {desde}–{hasta} de {total}</span>
            <select
              value={tamano}
              onChange={(e) => setTamano(Number(e.target.value))}
              className="input !w-auto !py-1 text-xs"
            >
              {TAMANOS.map((t) => (
                <option key={t} value={t}>{t} por página</option>
              ))}
            </select>
          </div>
          <div className="flex items-center gap-2">
            <button
              onClick={() => setPagina((p) => Math.max(1, p - 1))}
              disabled={pagina <= 1}
              className="btn-secondary !px-2 !py-1"
              title="Anterior"
            >
              <ChevronLeft size={14} />
            </button>
            <span className="text-ink-secondary">Página {pagina} de {totalPaginas}</span>
            <button
              onClick={() => setPagina((p) => Math.min(totalPaginas, p + 1))}
              disabled={pagina >= totalPaginas}
              className="btn-secondary !px-2 !py-1"
              title="Siguiente"
            >
              <ChevronRight size={14} />
            </button>
          </div>
        </div>
      </div>

      {(creando || editando) && (
        <EmpleadoModal
          empleado={editando}
          areas={areas}
          esAdmin={esAdmin}
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

function EmpleadoModal({
  empleado,
  areas,
  esAdmin,
  onClose,
  onOk,
  onError
}: {
  empleado: EmpleadoDto | null;
  areas: AreaDto[];
  esAdmin?: boolean;
  onClose: () => void;
  onOk: (m: string) => void;
  onError: (e: string) => void;
}) {
  const [legajo, setLegajo] = useState(empleado?.legajo ?? 0);
  const [nombre, setNombre] = useState(empleado?.nombre ?? '');
  const [apellido, setApellido] = useState(empleado?.apellido ?? '');
  const [dni, setDni] = useState(empleado?.dni ?? '');
  const [cuil, setCuil] = useState(empleado?.cuil ?? '');
  const [correo, setCorreo] = useState(empleado?.correo ?? '');
  const [areaId, setAreaId] = useState(empleado?.areaId ?? '');
  const [roles, setRoles] = useState<string[]>(empleado?.roles ?? ['Empleado']);
  const [guardando, setGuardando] = useState(false);

  const toggleRol = (r: string) =>
    setRoles((prev) => (prev.includes(r) ? prev.filter((x) => x !== r) : [...prev, r]));

  const guardar = async () => {
    setGuardando(true);
    try {
      if (empleado) {
        await adminApi.actualizarEmpleado(empleado.id, {
          nombre,
          apellido,
          dni: dni || undefined,
          cuil: cuil || undefined,
          areaId: areaId || null
        });
        if (esAdmin) await adminApi.setRoles(empleado.id, roles);
        onOk('Empleado actualizado.');
      } else {
        await adminApi.crearEmpleado({
          legajo,
          nombre,
          apellido,
          dni: dni || undefined,
          cuil: cuil || undefined,
          correo,
          areaId: areaId || null,
          roles
        });
        onOk('Empleado creado.');
      }
    } catch (e: any) {
      onError(e.response?.data?.error ?? 'No se pudo guardar.');
    } finally {
      setGuardando(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4" onClick={onClose}>
      <div className="card max-h-[90vh] w-full max-w-md overflow-y-auto" onClick={(e) => e.stopPropagation()}>
        <div className="mb-3 flex items-center justify-between">
          <h2 className="text-lg font-bold">{empleado ? 'Editar empleado' : 'Nuevo empleado'}</h2>
          <button onClick={onClose}><X size={18} /></button>
        </div>
        <div className="space-y-3">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="label">Legajo</label>
              <input type="number" value={legajo} disabled={!!empleado} onChange={(e) => setLegajo(Number(e.target.value))} className="input" />
            </div>
            <div>
              <label className="label">DNI</label>
              <input value={dni} onChange={(e) => setDni(e.target.value)} className="input" />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="label">Nombre</label>
              <input value={nombre} onChange={(e) => setNombre(e.target.value)} className="input" />
            </div>
            <div>
              <label className="label">Apellido</label>
              <input value={apellido} onChange={(e) => setApellido(e.target.value)} className="input" />
            </div>
          </div>
          {!empleado && (
            <>
              <div>
                <label className="label">Correo @iupa.edu.ar</label>
                <input value={correo} onChange={(e) => setCorreo(e.target.value)} className="input" />
              </div>
              <div>
                <label className="label">CUIL</label>
                <input value={cuil} onChange={(e) => setCuil(e.target.value)} className="input" />
              </div>
            </>
          )}
          <div>
            <label className="label">Área</label>
            <select value={areaId} onChange={(e) => setAreaId(e.target.value)} className="input">
              <option value="">Sin área</option>
              {areas.map((a) => (
                <option key={a.id} value={a.id}>{a.nombre}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="label">Roles</label>
            <div className="flex flex-wrap gap-2">
              {ROLES.map((r) => (
                <button
                  key={r}
                  onClick={() => toggleRol(r)}
                  disabled={!esAdmin}
                  className={`badge ${roles.includes(r) ? 'bg-accent text-accent-ink' : 'bg-surface-soft text-ink-secondary'}`}
                >
                  {ETIQUETA_ROL[r]}
                </button>
              ))}
            </div>
          </div>
          <button onClick={guardar} disabled={guardando} className="btn-primary w-full">
            {guardando ? 'Guardando...' : 'Guardar'}
          </button>
        </div>
      </div>
    </div>
  );
}