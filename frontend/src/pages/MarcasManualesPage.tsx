import { useEffect, useMemo, useState } from 'react';
import { LogIn, LogOut, Plus, RefreshCw } from 'lucide-react';
import { fichadasApi } from '../api';
import SelectBusqueda, { type OpcionSelectBusqueda } from '../components/SelectBusqueda';
import { esResponsable, useAuthStore } from '../store/authStore';
import type { HomeOfficeEmpleadoDto, MarcaManualDto } from '../types';
import { formatFechaHora } from '../utils';

export default function MarcasManualesPage() {
  const usuario = useAuthStore((s) => s.usuario);
  const esAdmin = esResponsable(usuario?.roles);
  const [empleados, setEmpleados] = useState<HomeOfficeEmpleadoDto[]>([]);
  const [marcas, setMarcas] = useState<MarcaManualDto[]>([]);
  const [empleadoFiltro, setEmpleadoFiltro] = useState('');
  const [tipo, setTipo] = useState<'entrada' | 'salida'>('entrada');
  const [fecha, setFecha] = useState(() => new Date().toISOString().slice(0, 10));
  const [hora, setHora] = useState(() => new Date().toTimeString().slice(0, 5));
  const [empleadoCarga, setEmpleadoCarga] = useState('');
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const opcionesEmpleados: OpcionSelectBusqueda[] = useMemo(
    () =>
      empleados.map((e) => ({
        id: e.id,
        etiqueta: `${e.apellido}, ${e.nombre} (leg. ${e.legajo})`
      })),
    [empleados]
  );

  useEffect(() => {
    if (esAdmin) {
      fichadasApi.empleadosHomeOffice().then(setEmpleados).catch(() => {});
    }
  }, [esAdmin]);

  const cargar = (empleadoId?: string) => {
    fichadasApi
      .marcasManuales(esAdmin && empleadoId ? { empleadoId, desde: `${new Date().getFullYear()}-01-01`, hasta: `${new Date().getFullYear()}-12-31` } : {})
      .then(setMarcas)
      .catch(() => {});
  };

  useEffect(() => {
    cargar(empleadoFiltro);
  }, [empleadoFiltro]);

  const nombreEmpleado = (id: string) => {
    const e = empleados.find((x) => x.id === id);
    return e ? `${e.apellido}, ${e.nombre} (leg. ${e.legajo})` : null;
  };

  const crear = async () => {
    setMensaje(null);
    setError(null);
    if (!fecha || !hora) {
      setError('Elegí fecha y hora.');
      return;
    }
    const destino = esAdmin ? (empleadoCarga || usuario?.empleadoId || null) : null;
    try {
      await fichadasApi.crearMarcaManual({
        empleadoId: destino,
        fechaHora: `${fecha}T${hora}:00`,
        tipo
      });
      setMensaje('Marca cargada correctamente.');
      cargar(empleadoFiltro);
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo cargar la marca.');
    }
  };

  const marcasFiltradas = useMemo(() => {
    if (!esAdmin || !empleadoFiltro) return marcas;
    return marcas.filter((m) => m.empleadoId === empleadoFiltro);
  }, [marcas, empleadoFiltro, esAdmin]);

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Marcas manuales</h1>
        <p className="text-sm text-ink-secondary">
          Carga de ingresos y egresos para personal en home office o que no puede marcar en el reloj biométrico.
        </p>
      </div>

      {mensaje && <p className="rounded-lg tint-success px-3 py-2 text-sm">{mensaje}</p>}
      {error && <p className="rounded-lg tint-danger px-3 py-2 text-sm">{error}</p>}

      <div className="card space-y-3">
        <h2 className="font-semibold">Registrar marca</h2>
        {esAdmin && (
          <SelectBusqueda
            opciones={opcionesEmpleados}
            valor={empleadoCarga}
            onChange={setEmpleadoCarga}
            opcionVacia={`Para mí (${usuario?.nombre ?? usuario?.correo})`}
            placeholder="Buscar empleado..."
          />
        )}
        <div className="flex flex-wrap items-end gap-3">
          <div>
            <label className="label">Tipo</label>
            <div className="flex gap-2">
              <button
                onClick={() => setTipo('entrada')}
                className={`btn ${tipo === 'entrada' ? 'bg-accent text-accent-ink' : 'btn-secondary'}`}
              >
                <LogIn size={16} /> Ingreso
              </button>
              <button
                onClick={() => setTipo('salida')}
                className={`btn ${tipo === 'salida' ? 'bg-accent text-accent-ink' : 'btn-secondary'}`}
              >
                <LogOut size={16} /> Egreso
              </button>
            </div>
          </div>
          <div>
            <label className="label">Fecha</label>
            <input type="date" value={fecha} onChange={(e) => setFecha(e.target.value)} className="input" />
          </div>
          <div>
            <label className="label">Hora</label>
            <input type="time" value={hora} onChange={(e) => setHora(e.target.value)} className="input" />
          </div>
          <button onClick={crear} className="btn-primary">
            <Plus size={16} /> Cargar
          </button>
        </div>
      </div>

      <div className="card">
        <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
          <h2 className="font-semibold">Marcas cargadas</h2>
          <div className="flex items-center gap-2">
            {esAdmin && (
              <SelectBusqueda
                opciones={opcionesEmpleados}
                valor={empleadoFiltro}
                onChange={setEmpleadoFiltro}
                opcionVacia="Todos"
                placeholder="Buscar empleado..."
                className="w-64"
              />
            )}
            <button onClick={() => cargar(empleadoFiltro)} className="btn-secondary !px-2 !py-1 text-xs">
              <RefreshCw size={14} /> Actualizar
            </button>
          </div>
        </div>
        {marcasFiltradas.length === 0 ? (
          <p className="text-sm text-ink-secondary">No hay marcas manuales en el período.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="border-b border-soft text-xs uppercase tracking-wide text-ink-muted">
                  <th className="py-2 pr-3">Fecha y hora</th>
                  {esAdmin && <th className="py-2 pr-3">Empleado</th>}
                  <th className="py-2 pr-3">Tipo</th>
                  <th className="py-2">Origen</th>
                </tr>
              </thead>
              <tbody>
                {marcasFiltradas.map((m) => (
                  <tr key={m.id} className="border-b border-soft last:border-0">
                    <td className="py-2 pr-3">{formatFechaHora(m.fechaHora)}</td>
                    {esAdmin && <td className="py-2 pr-3">{nombreEmpleado(m.empleadoId) ?? '—'}</td>}
                    <td className="py-2 pr-3">
                      <span className={`badge ${m.tipo === 'entrada' ? 'tint-success text-success' : 'tint-warning'}`}>
                        {m.tipo === 'entrada' ? 'Ingreso' : 'Egreso'}
                      </span>
                    </td>
                    <td className="py-2 text-ink-muted">{m.origen}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}