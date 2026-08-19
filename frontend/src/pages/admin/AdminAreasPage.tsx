import { useEffect, useState } from 'react';
import { Pencil, Plus, UserCheck, UserX, X } from 'lucide-react';
import { adminApi } from '../../api';
import type { AreaDto, EmpleadoDto, OrganigramaNodoDto, RelacionACargoDto } from '../../types';

export default function AdminAreasPage() {
  const [areas, setAreas] = useState<AreaDto[]>([]);
  const [empleados, setEmpleados] = useState<EmpleadoDto[]>([]);
  const [organigrama, setOrganigrama] = useState<OrganigramaNodoDto[]>([]);
  const [aCargo, setACargo] = useState<RelacionACargoDto[]>([]);
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const cargar = () => {
    adminApi.areas().then(setAreas).catch(() => {});
    adminApi.empleados({ tamano: 5000 }).then((r) => setEmpleados(r.items)).catch(() => {});
    adminApi.organigrama().then(setOrganigrama).catch(() => {});
  };

  useEffect(() => { cargar(); }, []);

  const verACargo = async (responsableId: string) => {
    setACargo(await adminApi.aCargo(responsableId));
  };

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Áreas y relaciones</h1>
        <p className="text-sm text-ink-secondary">Estructura orgánica y responsables directos.</p>
      </div>

      {mensaje && <p className="rounded-lg tint-success px-3 py-2 text-sm">{mensaje}</p>}
      {error && <p className="rounded-lg tint-danger px-3 py-2 text-sm">{error}</p>}

      <div className="grid gap-6 lg:grid-cols-2">
        <div className="card">
          <div className="mb-3 flex items-center justify-between">
            <h2 className="font-semibold">Áreas</h2>
            <CrearArea onOk={setMensaje} onError={setError} onCreada={cargar} />
          </div>
          <ul className="space-y-1">
            {areas.map((a) => (
              <li key={a.id} className="rounded-lg px-3 py-2 text-sm hover:bg-surface-soft">
                <div className="flex items-center justify-between">
                  <span>
                    {a.nombre} <span className="text-xs text-ink-muted">({a.codigo})</span>
                    {!a.activa && <span className="ml-2 badge bg-surface-alt text-ink-secondary">Inactiva</span>}
                  </span>
                  <EditarArea area={a} areas={areas} onOk={setMensaje} onError={setError} onGuardada={cargar} />
                </div>
              </li>
            ))}
          </ul>
        </div>

        <div className="card">
          <h2 className="mb-3 font-semibold">Asignar responsable</h2>
          <AsignarResponsable empleados={empleados} onOk={setMensaje} onError={setError} onAsignada={cargar} />
          <h3 className="mb-2 mt-6 font-semibold text-sm">Organigrama</h3>
          <Organigrama nodos={organigrama} onVerACargo={verACargo} />
        </div>
      </div>

      {aCargo.length > 0 && (
        <div className="card">
          <h2 className="mb-3 font-semibold">Relaciones vigentes</h2>
          <ul className="divide-y divide-soft">
            {aCargo.map((r) => (
              <li key={r.id} className="flex items-center justify-between py-2 text-sm">
                <span>
                  <UserCheck size={14} className="mr-1 inline text-success" />
                  {r.responsableNombre} → {r.empleadoNombre}
                  {r.autorizaMarcas ? ' · autoriza marcas' : ''}
                </span>
                <button
                  onClick={async () => {
                    await adminApi.quitarRelacion(r.id);
                    setACargo([]);
                    cargar();
                  }}
                  className="text-danger hover:text-danger"
                >
                  <UserX size={16} />
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

function CrearArea({ onOk, onError, onCreada }: { onOk: (m: string) => void; onError: (e: string) => void; onCreada: () => void }) {
  const [abierto, setAbierto] = useState(false);
  const [nombre, setNombre] = useState('');
  const [codigo, setCodigo] = useState('');

  const crear = async () => {
    try {
      await adminApi.crearArea({ nombre, codigo });
      setNombre('');
      setCodigo('');
      setAbierto(false);
      onOk('Área creada.');
      onCreada();
    } catch (e: any) {
      onError(e.response?.data?.error ?? 'No se pudo crear el área.');
    }
  };

  if (!abierto) {
    return (
      <button onClick={() => setAbierto(true)} className="btn-secondary !px-2 !py-1 text-xs">
        <Plus size={14} /> Nueva
      </button>
    );
  }

  return (
    <div className="flex gap-2">
      <input value={nombre} onChange={(e) => setNombre(e.target.value)} className="input !py-1 text-xs" placeholder="Nombre" />
      <input value={codigo} onChange={(e) => setCodigo(e.target.value)} className="input !w-20 !py-1 text-xs" placeholder="Código" />
      <button onClick={crear} className="btn-primary !px-2 !py-1 text-xs">Crear</button>
      <button onClick={() => setAbierto(false)} className="btn-secondary !px-2 !py-1"><X size={14} /></button>
    </div>
  );
}

function EditarArea({
  area,
  areas,
  onOk,
  onError,
  onGuardada
}: {
  area: AreaDto;
  areas: AreaDto[];
  onOk: (m: string) => void;
  onError: (e: string) => void;
  onGuardada: () => void;
}) {
  const [abierto, setAbierto] = useState(false);
  const [nombre, setNombre] = useState(area.nombre);
  const [codigo, setCodigo] = useState(area.codigo);
  const [areaPadreId, setAreaPadreId] = useState<string>(area.areaPadreId ?? '');
  const [activa, setActiva] = useState(area.activa);

  const guardar = async () => {
    try {
      await adminApi.actualizarArea(area.id, {
        nombre,
        codigo,
        areaPadreId: areaPadreId || null,
        activa
      });
      setAbierto(false);
      onOk('Área actualizada.');
      onGuardada();
    } catch (e: any) {
      onError(e.response?.data?.error ?? 'No se pudo actualizar el área.');
    }
  };

  if (!abierto) {
    return (
      <button
        onClick={() => {
          setNombre(area.nombre);
          setCodigo(area.codigo);
          setAreaPadreId(area.areaPadreId ?? '');
          setActiva(area.activa);
          setAbierto(true);
        }}
        className="btn-secondary !px-2 !py-1 text-xs"
        title="Editar área"
      >
        <Pencil size={14} />
      </button>
    );
  }

  return (
    <div className="mt-2 space-y-2 rounded-lg border border-soft bg-surface-alt p-3">
      <div className="flex gap-2">
        <input value={nombre} onChange={(e) => setNombre(e.target.value)} className="input !py-1 text-xs" placeholder="Nombre" />
        <input value={codigo} onChange={(e) => setCodigo(e.target.value)} className="input !w-20 !py-1 text-xs" placeholder="Código" />
      </div>
      <select value={areaPadreId} onChange={(e) => setAreaPadreId(e.target.value)} className="input !py-1 text-xs">
        <option value="">Sin área padre (raíz)</option>
        {areas
          .filter((o) => o.id !== area.id)
          .map((o) => (
            <option key={o.id} value={o.id}>
              {o.nombre}
            </option>
          ))}
      </select>
      <label className="flex items-center gap-2 text-xs">
        <input type="checkbox" checked={activa} onChange={(e) => setActiva(e.target.checked)} />
        Área activa
      </label>
      <div className="flex gap-2">
        <button onClick={guardar} className="btn-primary !px-2 !py-1 text-xs">Guardar</button>
        <button onClick={() => setAbierto(false)} className="btn-secondary !px-2 !py-1 text-xs">
          <X size={14} />
        </button>
      </div>
    </div>
  );
}

function AsignarResponsable({
  empleados,
  onOk,
  onError,
  onAsignada
}: {
  empleados: EmpleadoDto[];
  onOk: (m: string) => void;
  onError: (e: string) => void;
  onAsignada: () => void;
}) {
  const [responsableId, setResponsableId] = useState('');
  const [empleadoId, setEmpleadoId] = useState('');
  const [autoriza, setAutoriza] = useState(true);

  const asignar = async () => {
    if (!responsableId || !empleadoId) {
      onError('Elegí responsable y empleado.');
      return;
    }
    try {
      await adminApi.asignarResponsable(responsableId, empleadoId, autoriza);
      setResponsableId('');
      setEmpleadoId('');
      onOk('Relación asignada.');
      onAsignada();
    } catch (e: any) {
      onError(e.response?.data?.error ?? 'No se pudo asignar.');
    }
  };

  return (
    <div className="space-y-2">
      <select value={responsableId} onChange={(e) => setResponsableId(e.target.value)} className="input">
        <option value="">Responsable...</option>
        {empleados.filter((e) => e.roles.some((r) => ['Responsable', 'Rrhh', 'Administrador', 'Direccion'].includes(r))).map((e) => (
          <option key={e.id} value={e.id}>{e.apellido}, {e.nombre}</option>
        ))}
      </select>
      <select value={empleadoId} onChange={(e) => setEmpleadoId(e.target.value)} className="input">
        <option value="">Empleado a cargo...</option>
        {empleados.map((e) => (
          <option key={e.id} value={e.id}>{e.apellido}, {e.nombre} (leg. {e.legajo})</option>
        ))}
      </select>
      <label className="flex items-center gap-2 text-sm">
        <input type="checkbox" checked={autoriza} onChange={(e) => setAutoriza(e.target.checked)} />
        Autoriza marcas de reloj
      </label>
      <button onClick={asignar} className="btn-primary w-full">Asignar</button>
    </div>
  );
}

function Organigrama({ nodos, onVerACargo }: { nodos: OrganigramaNodoDto[]; onVerACargo: (id: string) => void }) {
  return (
    <ul className="space-y-1 text-sm">
      {nodos.map((n) => (
        <li key={n.id}>
          <div className="flex items-center justify-between rounded-lg px-3 py-2">
            <span className="font-medium">
              {n.nombre} <span className="text-xs text-ink-muted">({n.codigo})</span>
            </span>
          </div>
          {(n.hijas?.length > 0 || n.empleados?.length > 0) && (
            <ul className="ml-4 space-y-1 border-l border-soft pl-2">
              {n.empleados?.map((e) => (
                <li key={e.id}>
                  <button
                    onClick={() => onVerACargo(e.id)}
                    className="w-full rounded-lg px-3 py-1.5 text-left hover:bg-surface-soft"
                  >
                    {e.apellido}, {e.nombre} <span className="text-xs text-ink-muted">(leg. {e.legajo})</span>
                  </button>
                </li>
              ))}
              {n.hijas?.length > 0 && <Organigrama nodos={n.hijas} onVerACargo={onVerACargo} />}
            </ul>
          )}
        </li>
      ))}
    </ul>
  );
}