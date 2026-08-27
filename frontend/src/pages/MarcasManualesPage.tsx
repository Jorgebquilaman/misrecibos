import { useEffect, useMemo, useState } from 'react';
import { LogIn, LogOut, Pencil, Plus, RefreshCw, Trash2 } from 'lucide-react';
import { fichadasApi } from '../api';
import SelectBusqueda, { type OpcionSelectBusqueda } from '../components/SelectBusqueda';
import { esResponsable, useAuthStore } from '../store/authStore';
import type { HomeOfficeEmpleadoDto, MarcaManualDto } from '../types';
import { formatFechaHora, mesActual } from '../utils';

export default function MarcasManualesPage() {
  const usuario = useAuthStore((s) => s.usuario);
  const esAdmin = esResponsable(usuario?.roles);
  const [empleados, setEmpleados] = useState<HomeOfficeEmpleadoDto[]>([]);
  const [marcas, setMarcas] = useState<MarcaManualDto[]>([]);
  const [empleadoFiltro, setEmpleadoFiltro] = useState('');
  const [anio, setAnio] = useState(mesActual().anio);
  const [mes, setMes] = useState(mesActual().mes);
  const [tipo, setTipo] = useState<'entrada' | 'salida'>('entrada');
  const [fecha, setFecha] = useState(() => new Date().toISOString().slice(0, 10));
  const [hora, setHora] = useState(() => new Date().toTimeString().slice(0, 5));
  const [empleadoCarga, setEmpleadoCarga] = useState('');
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [editando, setEditando] = useState<{ id: string; fecha: string; hora: string; tipo: 'entrada' | 'salida' } | null>(null);

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
    const ultimoDia = new Date(anio, mes, 0).getDate();
    fichadasApi
      .marcasManuales({
        desde: `${anio}-${String(mes).padStart(2, '0')}-01`,
        hasta: `${anio}-${String(mes).padStart(2, '0')}-${String(ultimoDia).padStart(2, '0')}`,
        empleadoId: esAdmin && empleadoId ? empleadoId : undefined
      })
      .then(setMarcas)
      .catch(() => {});
  };

  useEffect(() => {
    cargar(empleadoFiltro);
  }, [empleadoFiltro, anio, mes]);

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

  const empezarEdicion = (m: MarcaManualDto) => {
    setMensaje(null);
    setError(null);
    const [f, h] = m.fechaHora.split('T');
    setEditando({ id: m.id, fecha: f, hora: h.slice(0, 5), tipo: m.tipo === 'entrada' ? 'entrada' : 'salida' });
  };

  const guardarEdicion = async () => {
    if (!editando) return;
    setError(null);
    try {
      await fichadasApi.editarMarcaManual(editando.id, {
        fechaHora: `${editando.fecha}T${editando.hora}:00`,
        tipo: editando.tipo
      });
      setMensaje('Marca editada correctamente.');
      setEditando(null);
      cargar(empleadoFiltro);
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo editar la marca.');
    }
  };

  const eliminar = async (id: string) => {
    if (!confirm('¿Eliminar esta marca? También se borra de la base del reloj.')) return;
    setMensaje(null);
    setError(null);
    try {
      await fichadasApi.eliminarMarcaManual(id);
      setMensaje('Marca eliminada correctamente.');
      cargar(empleadoFiltro);
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo eliminar la marca.');
    }
  };

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
            <input
              type="month"
              value={`${anio}-${String(mes).padStart(2, '0')}`}
              onChange={(e) => {
                const [a, m] = e.target.value.split('-').map(Number);
                setAnio(a);
                setMes(m);
              }}
              className="input !w-auto"
            />
            <button onClick={() => { setAnio(mesActual().anio); setMes(mesActual().mes); }} className="btn-secondary !px-2 !py-1 text-xs">
              <RefreshCw size={14} /> Este mes
            </button>
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
                  <th className="py-2 pr-3">Origen</th>
                  <th className="py-2"></th>
                </tr>
              </thead>
              <tbody>
                {marcasFiltradas.map((m) => (
                  <tr key={m.id} className="border-b border-soft last:border-0">
                    <td className="py-2 pr-3">
                      {editando?.id === m.id ? (
                        <div className="flex flex-wrap items-center gap-1">
                          <input
                            type="date"
                            value={editando.fecha}
                            onChange={(e) => setEditando({ ...editando, fecha: e.target.value })}
                            className="input !w-auto !px-2 !py-1 text-xs"
                          />
                          <input
                            type="time"
                            value={editando.hora}
                            onChange={(e) => setEditando({ ...editando, hora: e.target.value })}
                            className="input !w-auto !px-2 !py-1 text-xs"
                          />
                        </div>
                      ) : (
                        formatFechaHora(m.fechaHora)
                      )}
                    </td>
                    {esAdmin && <td className="py-2 pr-3">{nombreEmpleado(m.empleadoId) ?? '—'}</td>}
                    <td className="py-2 pr-3">
                      {editando?.id === m.id ? (
                        <select
                          value={editando.tipo}
                          onChange={(e) => setEditando({ ...editando, tipo: e.target.value as 'entrada' | 'salida' })}
                          className="input !w-auto !px-2 !py-1 text-xs"
                        >
                          <option value="entrada">Ingreso</option>
                          <option value="salida">Egreso</option>
                        </select>
                      ) : (
                        <span className={`badge ${m.tipo === 'entrada' ? 'tint-success text-success' : 'tint-warning'}`}>
                          {m.tipo === 'entrada' ? 'Ingreso' : 'Egreso'}
                        </span>
                      )}
                    </td>
                    <td className="py-2 pr-3 text-ink-muted">{m.origen}</td>
                    <td className="py-2">
                      {editando?.id === m.id ? (
                        <div className="flex items-center gap-1">
                          <button onClick={guardarEdicion} className="btn-primary !px-3 !py-1 text-xs">Guardar</button>
                          <button onClick={() => setEditando(null)} className="btn-secondary !px-3 !py-1 text-xs">Cancelar</button>
                        </div>
                      ) : (
                        <div className="flex items-center gap-1">
                          <button
                            onClick={() => empezarEdicion(m)}
                            className="btn-secondary !px-2 !py-1"
                            title="Editar fecha/hora y tipo"
                          >
                            <Pencil size={14} />
                          </button>
                          <button
                            onClick={() => eliminar(m.id)}
                            className="btn-danger !px-2 !py-1"
                            title="Eliminar marca"
                          >
                            <Trash2 size={14} />
                          </button>
                        </div>
                      )}
                    </td>
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