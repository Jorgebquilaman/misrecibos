import { useEffect, useMemo, useState } from 'react';
import { Loader2, LogIn, LogOut, MapPin, Pencil, Plus, RefreshCw, Trash2 } from 'lucide-react';
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
  const [geoEstado, setGeoEstado] = useState<'pidiendo' | 'ok' | 'denegada' | 'no-disponible'>('pidiendo');
  const [cargandoMarca, setCargandoMarca] = useState<'geo' | 'guardando' | null>(null);
  const [cargandoLista, setCargandoLista] = useState(false);

  const pedirGeolocalizacion = () => {
    setGeoEstado('pidiendo');
    if (!('geolocation' in navigator)) {
      setGeoEstado('no-disponible');
      return;
    }
    navigator.geolocation.getCurrentPosition(
      () => setGeoEstado('ok'),
      () => setGeoEstado('denegada'),
      { enableHighAccuracy: true, timeout: 15000, maximumAge: 30000 }
    );
  };

  const obtenerPosicion = () => new Promise<GeolocationPosition>((resolve, reject) => {
    if (!('geolocation' in navigator)) {
      reject(new Error('no-disponible'));
      return;
    }
    navigator.geolocation.getCurrentPosition(resolve, reject, { enableHighAccuracy: true, timeout: 15000, maximumAge: 15000 });
  });

  useEffect(() => { pedirGeolocalizacion(); }, []);

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
    setCargandoLista(true);
    fichadasApi
      .marcasManuales({
        desde: `${anio}-${String(mes).padStart(2, '0')}-01`,
        hasta: `${anio}-${String(mes).padStart(2, '0')}-${String(ultimoDia).padStart(2, '0')}`,
        empleadoId: esAdmin && empleadoId ? empleadoId : undefined
      })
      .then(setMarcas)
      .catch(() => {})
      .finally(() => setCargandoLista(false));
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
    if (cargandoMarca) return;
    setCargandoMarca('geo');
    let coords: GeolocationPosition;
    try {
      coords = await obtenerPosicion();
    } catch {
      setError('No se pudo obtener tu ubicación. Verificá que la geolocalización esté habilitada en el navegador.');
      setCargandoMarca(null);
      return;
    }
    const destino = esAdmin ? (empleadoCarga || usuario?.empleadoId || null) : null;
    setCargandoMarca('guardando');
    try {
      const marca = await fichadasApi.crearMarcaManual({
        empleadoId: destino,
        fechaHora: `${fecha}T${hora}:00`,
        tipo,
        latitud: coords.coords.latitude,
        longitud: coords.coords.longitude
      });
      setMensaje(marca.edificio
        ? `Marca cargada correctamente — edificio detectado: ${marca.edificio}.`
        : 'Marca cargada correctamente (fuera del radio de los edificios registrados).');
      cargar(empleadoFiltro);
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo cargar la marca.');
    } finally {
      setCargandoMarca(null);
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

      {geoEstado !== 'ok' && (
        <div className="rounded-lg border border-danger/40 tint-danger p-4">
          <p className="font-semibold">Geolocalización requerida</p>
          <p className="mt-1 text-sm">
            {geoEstado === 'pidiendo' && 'Solicitando permiso de geolocalización...'}
            {geoEstado === 'denegada' &&
              'Para poder usar el servicio de marcas manuales es necesario que habilites la geolocalización en tu navegador. Sin ella no vas a poder registrar marcas.'}
            {geoEstado === 'no-disponible' &&
              'Tu navegador o dispositivo no admite geolocalización. Sin ella no vas a poder registrar marcas manuales.'}
          </p>
          {geoEstado === 'denegada' && (
            <button onClick={pedirGeolocalizacion} className="btn-secondary mt-3">
              <MapPin size={16} /> Reintentar habilitar geolocalización
            </button>
          )}
          {geoEstado === 'denegada' && (
            <p className="mt-2 text-xs text-ink-secondary">
              Si el navegador no vuelve a pedir permiso, habilitá la ubicación para este sitio desde el candado/ajustes de la barra de direcciones y recargá la página.
            </p>
          )}
        </div>
      )}

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
          <button
            onClick={crear}
            disabled={geoEstado !== 'ok' || cargandoMarca !== null}
            className={`btn-primary ${geoEstado !== 'ok' || cargandoMarca !== null ? 'opacity-60 cursor-not-allowed' : ''}`}
            title={geoEstado !== 'ok' ? 'Requiere geolocalización habilitada' : undefined}
          >
            {cargandoMarca ? (
              <>
                <Loader2 size={16} className="animate-spin" />
                {cargandoMarca === 'geo' ? 'Obteniendo ubicación...' : 'Guardando...'}
              </>
            ) : (
              <>
                <Plus size={16} /> Cargar
              </>
            )}
          </button>
        </div>
        {geoEstado === 'ok' && (
          <p className="flex items-center gap-1 text-xs text-ink-secondary">
            <MapPin size={12} /> Geolocalización activa: al registrar la marca se asociará al edificio más cercano (radio de detección 100 m aprox.).
          </p>
        )}
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
        {cargandoLista ? (
          <p className="flex items-center gap-2 py-4 text-sm text-ink-secondary">
            <Loader2 size={16} className="animate-spin" /> Cargando marcas...
          </p>
        ) : marcasFiltradas.length === 0 ? (
          <p className="text-sm text-ink-secondary">No hay marcas manuales en el período.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="border-b border-soft text-xs uppercase tracking-wide text-ink-muted">
                  <th className="py-2 pr-3">Fecha y hora</th>
                  {esAdmin && <th className="py-2 pr-3">Empleado</th>}
                  <th className="py-2 pr-3">Tipo</th>
                  <th className="py-2 pr-3">Lugar</th>
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
                    <td className="py-2 pr-3">
                      {m.edificio ? (
                        <span className="inline-flex items-center gap-0.5 rounded-pill bg-black/5 px-1.5 py-0.5 text-xs dark:bg-white/10">
                          <MapPin size={10} /> {m.edificio}
                        </span>
                      ) : m.latitud != null && m.longitud != null ? (
                        <a
                          href={`https://www.google.com/maps?q=${m.latitud},${m.longitud}`}
                          target="_blank"
                          rel="noreferrer"
                          className="inline-flex items-center gap-0.5 text-xs text-accent underline underline-offset-2"
                          title="Ver ubicación en el mapa"
                        >
                          <MapPin size={10} /> Ver en mapa
                        </a>
                      ) : (
                        <span className="text-ink-muted">—</span>
                      )}
                    </td>
                    <td className="py-2 pr-3">
                      <div className="flex flex-wrap items-center gap-1.5 text-ink-muted">
                        <span>{m.origen}</span>
                        {m.latitud != null && m.longitud != null && (
                          <span className="font-mono text-xs">
                            {m.latitud.toFixed(5)}, {m.longitud.toFixed(5)}
                          </span>
                        )}
                      </div>
                    </td>
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