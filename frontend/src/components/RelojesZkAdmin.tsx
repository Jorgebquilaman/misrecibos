import { useEffect, useState } from 'react';
import { Download, Fingerprint, Loader2, Pencil, Plug, Plus, Trash2, X } from 'lucide-react';
import { relojesZkApi } from '../api';
import type { RelojZkDescargaDto, RelojZkDto, RelojZkInfoDto, ResultadoDescargaRelojZkDto } from '../types';
import { formatFechaHora } from '../utils';

interface FormReloj {
  id?: string;
  nombre: string;
  ip: string;
  puerto: string;
  commKey: string;
  modo: 'directo' | 'mssql';
  activo: boolean;
}

const formVacio = (): FormReloj => ({ nombre: '', ip: '', puerto: '4370', commKey: '0', modo: 'directo', activo: true });

export default function RelojesZkAdmin() {
  const [relojes, setRelojes] = useState<RelojZkDto[]>([]);
  const [descargas, setDescargas] = useState<RelojZkDescargaDto[]>([]);
  const [form, setForm] = useState<FormReloj | null>(null);
  const [ocupado, setOcupado] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [detalle, setDetalle] = useState<{ relojNombre: string; resultado: ResultadoDescargaRelojZkDto } | null>(null);

  const cargar = () => {
    relojesZkApi.todos().then(setRelojes).catch(() => {});
    relojesZkApi.descargas().then(setDescargas).catch(() => {});
  };

  useEffect(() => { cargar(); }, []);

  const guardar = async () => {
    if (!form) return;
    if (!form.nombre.trim() || (form.modo === 'directo' && !form.ip.trim())) {
      setError('Completá nombre e IP del reloj.');
      return;
    }
    setOcupado('guardar');
    setError(null);
    const data = {
      nombre: form.nombre.trim(),
      ip: form.ip.trim() || '-',
      puerto: Number(form.puerto) || 4370,
      commKey: Number(form.commKey) || 0,
      modo: form.modo,
      activo: form.activo
    };
    try {
      if (form.id) await relojesZkApi.editar(form.id, data);
      else await relojesZkApi.crear(data);
      setMensaje(form.id ? 'Reloj actualizado.' : 'Reloj agregado.');
      setForm(null);
      cargar();
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo guardar el reloj.');
    } finally {
      setOcupado(null);
    }
  };

  const eliminar = async (r: RelojZkDto) => {
    if (!confirm(`¿Eliminar el reloj "${r.nombre}"? El historial de descargas se conserva.`)) return;
    setError(null);
    try {
      await relojesZkApi.eliminar(r.id);
      setMensaje('Reloj eliminado.');
      cargar();
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo eliminar el reloj.');
    }
  };

  const probar = async (r: RelojZkDto) => {
    setError(null);
    setMensaje(null);
    setOcupado(`probar-${r.id}`);
    try {
      const info: RelojZkInfoDto = await relojesZkApi.probar(r.id);
      const detalles = r.modo === 'mssql'
        ? `origen: ${info.nombre ?? 's/d'} (${info.serial ?? 's/d'}), ${info.marcas} marcas en los últimos 30 días` +
          (info.ultimaMarca ? `, última: ${formatFechaHora(info.ultimaMarca)}` : '')
        : `equipo: ${info.nombre ?? 'sin nombre'}, serial: ${info.serial ?? 's/d'}, ` +
          `${info.usuarios} usuarios y ${info.marcas} marcas en memoria`;
      setMensaje(`Conexión OK con "${r.nombre}" — ${detalles}.`);
    } catch (e: any) {
      setError(e.response?.data?.error ?? `No se pudo conectar a ${r.ip}:${r.puerto}.`);
    } finally {
      setOcupado(null);
    }
  };

  const descargar = async (r: RelojZkDto) => {
    if (!confirm(`¿Descargar las marcas del reloj "${r.nombre}"? Se importan solo las nuevas.`)) return;
    setError(null);
    setMensaje(null);
    setOcupado(`descargar-${r.id}`);
    try {
      const res = await relojesZkApi.descargar(r.id);
      setDetalle({ relojNombre: r.nombre, resultado: res });
      setMensaje(null);
      cargar();
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo descargar las marcas del reloj.');
    } finally {
      setOcupado(null);
    }
  };

  const vaciar = async (r: RelojZkDto) => {
    if (!confirm(
      `ATENCIÓN: esto borra TODOS los registros de asistencia de la memoria del reloj "${r.nombre}".\n\n` +
      'Las marcas ya descargadas al portal NO se borran.\n\n¿Confirmás el vaciado?'
    )) return;
    if (!confirm('Última confirmación: ¿vaciar la memoria de asistencia del reloj?')) return;
    setError(null);
    setMensaje(null);
    setOcupado(`vaciar-${r.id}`);
    try {
      await relojesZkApi.vaciar(r.id);
      setMensaje(`Memoria de asistencia del reloj "${r.nombre}" vaciada.`);
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo vaciar la memoria del reloj.');
    } finally {
      setOcupado(null);
    }
  };

  return (
    <div className="space-y-6">
      <div className="card space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="flex items-center gap-2 font-semibold">
            <Fingerprint size={18} /> Relojes ZKTeco
          </h2>
          {!form && (
            <button onClick={() => { setMensaje(null); setError(null); setForm(formVacio()); }} className="btn-primary">
              <Plus size={16} /> Agregar reloj
            </button>
          )}
        </div>
        <p className="text-sm text-ink-secondary">
          Dos modos de descarga: <b>Directo al equipo</b> (TCP, puerto 4370 por defecto) o <b>Vía ZKBio (MSSQL)</b>
          {' '}cuando el software ZKBio gestiona el equipo y no lo deja atender otra conexión. Probá la conexión antes
          de descargar; las marcas se importan con origen <span className="font-mono text-xs">reloj-zk</span> y solo se
          agregan las que no existan.
        </p>

        {mensaje && <p className="rounded-lg tint-success px-3 py-2 text-sm">{mensaje}</p>}
        {error && <p className="rounded-lg tint-danger px-3 py-2 text-sm">{error}</p>}

        {form && (
          <div className="space-y-3 rounded-lg border border-soft p-3">
            <div className="grid gap-3 sm:grid-cols-2">
              <div>
                <label className="label">Nombre</label>
                <input value={form.nombre} onChange={(e) => setForm({ ...form, nombre: e.target.value })} className="input" maxLength={200} placeholder="Ej: Reloj Entrada - Sede Central" />
              </div>
              <div>
                <label className="label">Modo de descarga</label>
                <select
                  value={form.modo}
                  onChange={(e) => setForm({ ...form, modo: e.target.value as 'directo' | 'mssql' })}
                  className="input"
                >
                  <option value="directo">Directo al equipo (TCP)</option>
                  <option value="mssql">Vía ZKBio (MSSQL)</option>
                </select>
              </div>
              {form.modo === 'directo' && (
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="label">IP</label>
                    <input value={form.ip} onChange={(e) => setForm({ ...form, ip: e.target.value })} className="input font-mono" placeholder="192.168.1.201" />
                  </div>
                  <div>
                    <label className="label">Puerto</label>
                    <input type="number" min={1} max={65535} value={form.puerto} onChange={(e) => setForm({ ...form, puerto: e.target.value })} className="input" />
                  </div>
                </div>
              )}
              {form.modo === 'directo' && (
                <div>
                  <label className="label">CommKey (0 = sin clave)</label>
                  <input type="number" min={0} max={999999} value={form.commKey} onChange={(e) => setForm({ ...form, commKey: e.target.value })} className="input" />
                </div>
              )}
              {form.modo === 'mssql' && (
                <p className="self-center text-xs text-ink-secondary sm:col-span-2">
                  Lee las marcas que el software ZKBio ya descargó a la base del reloj. No requiere acceso directo al equipo.
                </p>
              )}
              <label className="flex items-center gap-2 self-end text-sm">
                <input type="checkbox" checked={form.activo} onChange={(e) => setForm({ ...form, activo: e.target.checked })} />
                Activo
              </label>
            </div>
            <div className="flex flex-wrap gap-2">
              <button onClick={guardar} disabled={ocupado === 'guardar'} className="btn-primary">
                {ocupado === 'guardar' ? (
                  <><Loader2 size={16} className="animate-spin" /> Guardando...</>
                ) : form.id ? 'Guardar cambios' : 'Agregar reloj'}
              </button>
              <button onClick={() => setForm(null)} className="btn-secondary">Cancelar</button>
            </div>
          </div>
        )}

        {relojes.length > 0 ? (
          <div className="space-y-2">
            {relojes.map((r) => (
              <div key={r.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-soft p-3">
                <div className="min-w-0">
                  <p className="font-medium">
                    {r.nombre}{' '}
                    {!r.activo && <span className="badge tint-warning ml-1">inactivo</span>}
                  </p>
                  <p className="font-mono text-xs text-ink-muted">
                    {r.modo === 'mssql'
                      ? 'Vía ZKBio (MSSQL)'
                      : `${r.ip}:${r.puerto}${r.commKey > 0 ? ` · CommKey ${r.commKey}` : ''}`}
                  </p>
                  <p className="text-xs text-ink-secondary">
                    {r.ultimaDescarga
                      ? `Última descarga: ${formatFechaHora(r.ultimaDescarga)} (${r.ultimaCantidad} nuevas)`
                      : 'Nunca descargado'}
                  </p>
                </div>
                <div className="flex flex-wrap items-center gap-1">
                  <button
                    onClick={() => probar(r)}
                    disabled={ocupado !== null}
                    className="btn-secondary !px-2 !py-1 text-xs"
                    title="Probar conexión y ver info del equipo"
                  >
                    {ocupado === `probar-${r.id}` ? <Loader2 size={14} className="animate-spin" /> : <Plug size={14} />} Conexión
                  </button>
                  <button
                    onClick={() => descargar(r)}
                    disabled={ocupado !== null}
                    className="btn-primary !px-2 !py-1 text-xs"
                    title="Descargar marcas de asistencia"
                  >
                    {ocupado === `descargar-${r.id}` ? (
                      <><Loader2 size={14} className="animate-spin" /> Descargando...</>
                    ) : (
                      <><Download size={14} /> Descargar</>
                    )}
                  </button>
                  {r.modo === 'directo' && (
                    <button
                      onClick={() => vaciar(r)}
                      disabled={ocupado !== null}
                      className="btn-danger !px-2 !py-1 text-xs"
                      title="Vaciar registros de asistencia del equipo (doble confirmación)"
                    >
                      {ocupado === `vaciar-${r.id}` ? <Loader2 size={14} className="animate-spin" /> : <Trash2 size={14} />} Vaciar
                    </button>
                  )}
                  <button
                    onClick={() => {
                      setMensaje(null);
                      setError(null);
                      setForm({
                        id: r.id,
                        nombre: r.nombre,
                        ip: r.ip,
                        puerto: String(r.puerto),
                        commKey: String(r.commKey),
                        modo: r.modo,
                        activo: r.activo
                      });
                    }}
                    className="btn-secondary !px-2 !py-1"
                    title="Editar"
                  >
                    <Pencil size={14} />
                  </button>
                  <button onClick={() => eliminar(r)} className="btn-secondary !px-2 !py-1" title="Eliminar">
                    <Trash2 size={14} />
                  </button>
                </div>
              </div>
            ))}
          </div>
        ) : (
          !form && <p className="text-sm text-ink-secondary">No hay relojes cargados todavía.</p>
        )}
      </div>

      <div className="card">
        <h2 className="mb-3 font-semibold">Historial de descargas</h2>
        {descargas.length === 0 ? (
          <p className="text-sm text-ink-secondary">Sin descargas registradas.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="border-b border-soft text-xs uppercase tracking-wide text-ink-muted">
                  <th className="py-2 pr-3">Fecha</th>
                  <th className="py-2 pr-3">Reloj</th>
                  <th className="py-2 pr-3">Leídas</th>
                  <th className="py-2 pr-3">Nuevas</th>
                  <th className="py-2 pr-3">Duplicadas</th>
                  <th className="py-2 pr-3">Desconocidos</th>
                  <th className="py-2">Estado</th>
                </tr>
              </thead>
              <tbody>
                {descargas.map((d) => (
                  <tr key={d.id} className="border-b border-soft last:border-0">
                    <td className="py-2 pr-3">{formatFechaHora(d.fecha)}</td>
                    <td className="py-2 pr-3">{d.relojNombre}</td>
                    <td className="py-2 pr-3">{d.leidas}</td>
                    <td className="py-2 pr-3 font-medium">{d.nuevas}</td>
                    <td className="py-2 pr-3">{d.duplicadas}</td>
                    <td className="py-2 pr-3">{d.legajosDesconocidos}</td>
                    <td className="py-2">
                      <span className={`badge ${d.estado === 'Ok' ? 'tint-success text-success' : 'tint-danger text-danger'}`} title={d.mensaje ?? undefined}>
                        {d.estado}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {detalle && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
          onClick={() => setDetalle(null)}
        >
          <div
            className="max-h-[85vh] w-full max-w-3xl overflow-hidden rounded-xl bg-white shadow-2xl dark:bg-slate-900"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-2 border-b border-soft p-4">
              <div>
                <h3 className="flex items-center gap-2 font-semibold">
                  <Download size={18} /> Marcas enviadas por "{detalle.relojNombre}"
                </h3>
                <p className="mt-1 text-sm text-ink-secondary">
                  {detalle.resultado.leidas} marcas en el rango · {detalle.resultado.nuevas} nuevas importadas ·{' '}
                  {detalle.resultado.duplicadas} duplicadas · {detalle.resultado.desconocidos} legajos desconocidos
                </p>
              </div>
              <button onClick={() => setDetalle(null)} className="btn-secondary !px-2 !py-1" title="Cerrar">
                <X size={16} />
              </button>
            </div>
            <div className="max-h-[60vh] overflow-auto p-4">
              {detalle.resultado.marcas.length === 0 ? (
                <p className="text-sm text-ink-secondary">
                  El reloj no envió ninguna marca. Si su memoria dice tener registros pero no aparecen acá,
                  es la falla conocida del firmware.
                </p>
              ) : (
                <table className="w-full text-left text-sm">
                  <thead>
                    <tr className="border-b border-soft text-xs uppercase tracking-wide text-ink-muted">
                      <th className="py-2 pr-3">Fecha y hora</th>
                      <th className="py-2 pr-3">Legajo</th>
                      <th className="py-2 pr-3">Tipo</th>
                      <th className="py-2">Estado</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detalle.resultado.marcas.map((m, i) => (
                      <tr key={i} className="border-b border-soft last:border-0">
                        <td className="py-1.5 pr-3 font-mono text-xs">{formatFechaHora(m.fechaHora)}</td>
                        <td className="py-1.5 pr-3">{m.legajo}</td>
                        <td className="py-1.5 pr-3">{m.esSalida ? 'Salida' : 'Entrada'}</td>
                        <td className="py-1.5">
                          {!m.enRango ? (
                            <span
                              className="badge tint-warning"
                              title="La marca quedó fuera del rango de fechas de esta descarga (por ejemplo, fecha inválida del reloj)"
                            >
                              fuera de rango
                            </span>
                          ) : m.legajoDesconocido ? (
                            <span
                              className="badge tint-warning"
                              title="El legajo no existe o está inactivo en el portal"
                            >
                              legajo desconocido
                            </span>
                          ) : m.nueva ? (
                            <span className="badge tint-success text-success">nueva</span>
                          ) : (
                            <span className="badge bg-black/5 text-ink-secondary dark:bg-white/10">ya existía</span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
            <div className="flex justify-end border-t border-soft p-3">
              <button onClick={() => setDetalle(null)} className="btn-primary">
                Cerrar
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
