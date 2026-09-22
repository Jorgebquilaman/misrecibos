import { useEffect, useState } from 'react';
import { Building2, ChevronDown, ChevronRight, Crosshair, Map as MapIcon, Pencil, Plus, Trash2 } from 'lucide-react';
import { Circle, CircleMarker, MapContainer, TileLayer } from 'react-leaflet';
import { edificiosApi } from '../api';
import type { EdificioDto } from '../types';
import 'leaflet/dist/leaflet.css';

interface FormEdificio {
  id?: string;
  nombre: string;
  latitud: string;
  longitud: string;
  radioMetros: string;
  activo: boolean;
}

const formVacio = (): FormEdificio => ({ nombre: '', latitud: '', longitud: '', radioMetros: '100', activo: true });

export default function EdificiosAdmin() {
  const [edificios, setEdificios] = useState<EdificioDto[]>([]);
  const [form, setForm] = useState<FormEdificio | null>(null);
  const [cargando, setCargando] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [expandido, setExpandido] = useState<string | null>(null);

  const cargar = () => {
    edificiosApi.todos().then(setEdificios).catch(() => {});
  };

  useEffect(() => { cargar(); }, []);

  const obtenerUbicacionActual = async () => {
    setError(null);
    if (!('geolocation' in navigator)) {
      setError('Tu navegador no admite geolocalización.');
      return;
    }
    try {
      const pos = await new Promise<GeolocationPosition>((resolve, reject) =>
        navigator.geolocation.getCurrentPosition(resolve, reject, { enableHighAccuracy: true, timeout: 15000 })
      );
      setForm((f) => f ? ({
        ...f,
        latitud: pos.coords.latitude.toFixed(6),
        longitud: pos.coords.longitude.toFixed(6)
      }) : f);
      setMensaje('Ubicación capturada. Ajustá el nombre y guardá.');
    } catch {
      setError('No se pudo obtener la ubicación actual. Verificá que la geolocalización esté habilitada.');
    }
  };

  const guardar = async () => {
    if (!form) return;
    if (!form.nombre.trim() || !form.latitud || !form.longitud) {
      setError('Completá nombre y coordenadas (podés capturarlas con "Usar mi ubicación actual").');
      return;
    }
    setCargando(true);
    setError(null);
    const data = {
      nombre: form.nombre.trim(),
      latitud: Number(form.latitud),
      longitud: Number(form.longitud),
      radioMetros: Number(form.radioMetros) || 100,
      activo: form.activo
    };
    try {
      if (form.id) await edificiosApi.editar(form.id, data);
      else await edificiosApi.crear(data);
      setMensaje(form.id ? 'Edificio actualizado.' : 'Edificio agregado.');
      setForm(null);
      cargar();
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo guardar el edificio.');
    } finally {
      setCargando(false);
    }
  };

  const eliminar = async (id: string) => {
    if (!confirm('¿Eliminar este edificio?')) return;
    try {
      await edificiosApi.eliminar(id);
      setMensaje('Edificio eliminado.');
      cargar();
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo eliminar el edificio.');
    }
  };

  return (
    <div className="card space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="flex items-center gap-2 font-semibold">
          <Building2 size={18} /> Edificios con detección de ubicación
        </h2>
        {!form && (
          <button onClick={() => { setMensaje(null); setError(null); setForm(formVacio()); }} className="btn-primary">
            <Plus size={16} /> Agregar edificio
          </button>
        )}
      </div>
      <p className="text-sm text-ink-secondary">
        Parate en (o cerca de) el edificio, usá <b>"Usar mi ubicación actual"</b> para capturar las coordenadas GPS,
        ajustá el nombre y guardá. Una marca manual se asocia al edificio más cercano dentro de su radio de detección.
      </p>

      {mensaje && <p className="rounded-lg tint-success px-3 py-2 text-sm">{mensaje}</p>}
      {error && <p className="rounded-lg tint-danger px-3 py-2 text-sm">{error}</p>}

      {form && (
        <div className="space-y-3 rounded-lg border border-soft p-3">
          <div className="grid gap-3 sm:grid-cols-2">
            <div>
              <label className="label">Nombre</label>
              <input value={form.nombre} onChange={(e) => setForm({ ...form, nombre: e.target.value })} className="input" maxLength={200} placeholder="Ej: IUPA - Sede Central" />
            </div>
            <div>
              <label className="label">Radio de detección (metros)</label>
              <input type="number" min={10} value={form.radioMetros} onChange={(e) => setForm({ ...form, radioMetros: e.target.value })} className="input" />
            </div>
            <div className="grid grid-cols-2 gap-3 sm:col-span-2">
              <div>
                <label className="label">Latitud</label>
                <input value={form.latitud} onChange={(e) => setForm({ ...form, latitud: e.target.value })} className="input font-mono" placeholder="-39.027600" />
              </div>
              <div>
                <label className="label">Longitud</label>
                <input value={form.longitud} onChange={(e) => setForm({ ...form, longitud: e.target.value })} className="input font-mono" placeholder="-67.586300" />
              </div>
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            <button onClick={obtenerUbicacionActual} className="btn-secondary">
              <Crosshair size={16} /> Usar mi ubicación actual
            </button>
            <button onClick={guardar} disabled={cargando} className="btn-primary">
              {cargando ? 'Guardando...' : form.id ? 'Guardar cambios' : 'Agregar edificio'}
            </button>
            <button onClick={() => setForm(null)} className="btn-secondary">Cancelar</button>
          </div>
        </div>
      )}

      {edificios.length > 0 ? (
        <div className="space-y-2">
          {edificios.map((e) => (
            <div key={e.id} className="rounded-lg border border-soft p-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <button
                  onClick={() => setExpandido(expandido === e.id ? null : e.id)}
                  className="flex min-w-0 items-center gap-2 text-left"
                  title="Ver el edificio en el mapa con su radio de detección"
                >
                  {expandido === e.id ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
                  <div className="min-w-0">
                    <p className="font-medium">
                      {e.nombre}{' '}
                      {!e.activo && <span className="badge tint-warning ml-1">inactivo</span>}
                      <span className="ml-1 badge bg-black/5 text-ink-secondary dark:bg-white/10">radio {e.radioMetros} m</span>
                    </p>
                    <p className="font-mono text-xs text-ink-muted">{e.latitud.toFixed(6)}, {e.longitud.toFixed(6)}</p>
                  </div>
                </button>
                <div className="flex items-center gap-1">
                  <button
                    onClick={() => setExpandido(expandido === e.id ? null : e.id)}
                    className="btn-secondary !px-2 !py-1 text-xs"
                    title="Ver en el mapa"
                  >
                    <MapIcon size={14} /> Mapa
                  </button>
                  <button
                    onClick={() => {
                      setMensaje(null);
                      setError(null);
                      setForm({
                        id: e.id,
                        nombre: e.nombre,
                        latitud: e.latitud.toFixed(6),
                        longitud: e.longitud.toFixed(6),
                        radioMetros: String(e.radioMetros),
                        activo: e.activo
                      });
                    }}
                    className="btn-secondary !px-2 !py-1"
                    title="Editar"
                  >
                    <Pencil size={14} />
                  </button>
                  <button onClick={() => eliminar(e.id)} className="btn-danger !px-2 !py-1" title="Eliminar">
                    <Trash2 size={14} />
                  </button>
                </div>
              </div>
              {expandido === e.id && (
                <div className="mt-3 space-y-2">
                  <MapContainer
                    center={[e.latitud, e.longitud]}
                    zoom={17}
                    scrollWheelZoom={false}
                    style={{ height: 320, width: '100%', borderRadius: 8 }}
                  >
                    <TileLayer
                      url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
                      attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
                    />
                    <Circle
                      center={[e.latitud, e.longitud]}
                      radius={e.radioMetros}
                      pathOptions={{ color: '#2563eb', weight: 2, fillColor: '#2563eb', fillOpacity: 0.15 }}
                    />
                    <CircleMarker
                      center={[e.latitud, e.longitud]}
                      radius={7}
                      pathOptions={{ color: '#ffffff', weight: 2, fillColor: '#dc2626', fillOpacity: 1 }}
                    />
                  </MapContainer>
                  <p className="flex items-center gap-1 text-xs text-ink-secondary">
                    <MapIcon size={12} />
                    El círculo azul es el radio de detección ({e.radioMetros} m): una marca manual dentro del círculo
                    se asocia a este edificio. Podés mover y hacer zoom en el mapa.
                  </p>
                </div>
              )}
            </div>
          ))}
        </div>
      ) : (
        !form && <p className="text-sm text-ink-secondary">No hay edificios cargados todavía.</p>
      )}
    </div>
  );
}
