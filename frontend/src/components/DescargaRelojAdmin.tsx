import { useEffect, useState } from 'react';
import { Download, Loader2, Plug, RefreshCw, X } from 'lucide-react';
import { relojesZkApi } from '../api';
import type { RelojZkDescargaDto, RelojZkDto, ResultadoDescargaRelojZkDto } from '../types';
import { formatFechaHora } from '../utils';

/**
 * Interfaz de descarga del reloj biométrico:
 * - Estado de cada reloj en modo directo (conexión + última descarga)
 * - Descarga manual bajo demanda con vista del detalle marca por marca
 * - Historial de descargas
 * - Aclara que además hay un Worker Service que descarga y persiste en el MSSQL
 *   del proveedor cada N minutos con checkpoint antiduplicados.
 */
export default function DescargaRelojAdmin() {
  const [relojes, setRelojes] = useState<RelojZkDto[]>([]);
  const [descargas, setDescargas] = useState<RelojZkDescargaDto[]>([]);
  const [ocupado, setOcupado] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<{ reloj: RelojZkDto; texto: string } | null>(null);
  const [detalle, setDetalle] = useState<{ relojNombre: string; resultado: ResultadoDescargaRelojZkDto } | null>(null);

  const cargar = () => {
    relojesZkApi.todos().then(setRelojes).catch(() => {});
    relojesZkApi.descargas().then(setDescargas).catch(() => {});
  };

  useEffect(() => { cargar(); }, []);

  const probar = async (r: RelojZkDto) => {
    setOcupado(`probar-${r.id}`);
    setError(null);
    setInfo(null);
    try {
      const res = await relojesZkApi.probar(r.id);
      setInfo({
        reloj: r,
        texto: `${r.nombre}: equipo ${res.nombre ?? 's/d'}, serial ${res.serial ?? 's/d'}, ` +
          `${res.usuarios} usuarios y ${res.marcas} marcas en memoria`
      });
    } catch (e: unknown) {
      const err = e as { response?: { data?: { error?: string } } };
      setError(err.response?.data?.error ?? `No se pudo conectar a ${r.ip}:${r.puerto}.`);
    } finally {
      setOcupado(null);
    }
  };

  const descargar = async (r: RelojZkDto) => {
    if (!confirm(`¿Descargar las fichadas de "${r.nombre}"? Se importan solo las nuevas y se replican al MSSQL del proveedor.`)) return;
    setOcupado(`descargar-${r.id}`);
    setError(null);
    setInfo(null);
    try {
      const res = await relojesZkApi.descargar(r.id);
      setDetalle({ relojNombre: r.nombre, resultado: res });
      cargar();
    } catch (e: unknown) {
      const err = e as { response?: { data?: { error?: string } } };
      setError(err.response?.data?.error ?? `Error al descargar de ${r.ip}.`);
    } finally {
      setOcupado(null);
    }
  };

  return (
    <div className="space-y-4">
      <div className="card text-xs text-ink-secondary">
        <p className="font-medium text-sm text-ink-primary">Descarga automática</p>
        <p>
          Además de la descarga manual, un <strong>Worker Service</strong> corre cada 5 minutos:
          se conecta a cada reloj en modo directo, lee las marcaciones nuevas desde su último checkpoint
          y las inserta en la base SQL Server del software del proveedor (antiduplicado por legajo + fecha).
          Si el software oficial del proveedor también se conecta al mismo equipo, no hay conflicto:
          las sesiones son cortas (conectar, leer, salir).
        </p>
      </div>

      {error && <p className="text-sm text-danger">{error}</p>}

      {info && (
        <p className="rounded border border-soft p-2 text-xs text-success">✔ {info.texto}</p>
      )}

      <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
        {relojes.map((r) => (
          <div key={r.id} className="card space-y-2">
            <div className="flex items-center justify-between gap-2">
              <div className="min-w-0">
                <p className="flex items-center gap-2 font-semibold">
                  {r.nombre}
                  <span className="badge bg-black/5 text-[10px] text-ink-secondary dark:bg-white/10">
                    {r.ip}:{r.puerto} · {r.modo}
                  </span>
                  {!r.activo && <span className="badge tint-warning">inactivo</span>}
                </p>
                {r.ultimaDescarga && (
                  <p className="text-xs text-ink-secondary">
                    última descarga: {formatFechaHora(r.ultimaDescarga)} ({r.ultimaCantidad} marcas)
                  </p>
                )}
              </div>
              <div className="flex shrink-0 items-center gap-1">
                <button
                  onClick={() => probar(r)}
                  disabled={ocupado !== null}
                  className="btn-secondary !px-2 !py-1 text-xs"
                  title="Probar conexión"
                >
                  {ocupado === `probar-${r.id}` ? <Loader2 size={13} className="animate-spin" /> : <Plug size={13} />}
                </button>
                <button
                  onClick={() => descargar(r)}
                  disabled={ocupado !== null}
                  className="btn-primary flex items-center gap-1 text-xs"
                >
                  {ocupado === `descargar-${r.id}`
                    ? <Loader2 size={13} className="animate-spin" />
                    : <Download size={13} />} Descargar
                </button>
              </div>
            </div>
          </div>
        ))}
        {relojes.length === 0 && (
          <p className="text-sm text-ink-secondary">No hay relojes cargados todavía.</p>
        )}
      </div>

      <div className="card">
        <h2 className="mb-3 flex items-center gap-2 font-semibold"><RefreshCw size={15} /> Historial de descargas</h2>
        {descargas.length === 0 ? (
          <p className="text-sm text-ink-secondary">Sin descargas registradas.</p>
        ) : (
          <div className="max-h-64 overflow-auto">
            <table className="w-full text-left text-xs">
              <thead>
                <tr className="border-b border-soft text-[10px] uppercase tracking-wide text-ink-muted">
                  <th className="py-1.5 pr-3">Fecha</th>
                  <th className="py-1.5 pr-3">Reloj</th>
                  <th className="py-1.5 pr-3">Leídas</th>
                  <th className="py-1.5 pr-3">Nuevas</th>
                  <th className="py-1.5">Estado</th>
                </tr>
              </thead>
              <tbody>
                {descargas.map((d) => (
                  <tr key={d.id} className="border-b border-soft last:border-0">
                    <td className="py-1 pr-3">{formatFechaHora(d.fecha)}</td>
                    <td className="py-1 pr-3">{d.relojNombre}</td>
                    <td className="py-1 pr-3">{d.leidas}</td>
                    <td className="py-1 pr-3 font-medium">{d.nuevas}</td>
                    <td className="py-1">
                      <span className={`badge ${d.estado === 'Ok' ? 'tint-success text-success' : 'tint-danger text-danger'}`}>
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
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4" onClick={() => setDetalle(null)}>
          <div
            className="flex max-h-[85vh] w-full max-w-3xl flex-col overflow-hidden rounded-xl border border-soft bg-base shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-2 border-b border-soft p-4">
              <div>
                <h3 className="font-semibold">Marcas enviadas por "{detalle.relojNombre}"</h3>
                <p className="mt-1 text-sm text-ink-secondary">
                  {detalle.resultado.leidas} en el rango · {detalle.resultado.nuevas} nuevas ·{' '}
                  {detalle.resultado.duplicadas} duplicadas · {detalle.resultado.desconocidos} desconocidos
                </p>
              </div>
              <button onClick={() => setDetalle(null)} className="btn-secondary !px-2 !py-1"><X size={16} /></button>
            </div>
            <div className="max-h-[60vh] overflow-auto p-4">
              {detalle.resultado.marcas.length === 0 ? (
                <p className="text-sm text-ink-secondary">El reloj no envió ninguna marca en el rango.</p>
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
                            <span className="badge tint-warning" title="Fuera del rango de fechas">fuera de rango</span>
                          ) : m.legajoDesconocido ? (
                            <span className="badge tint-warning">legajo desconocido</span>
                          ) : m.nueva ? (
                            <span className="badge tint-success text-success">nueva</span>
                          ) : (
                            <span className="badge bg-black/5 text-ink-secondary">ya existía</span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
            <div className="flex justify-end border-t border-soft p-3">
              <button onClick={() => setDetalle(null)} className="btn-primary">Cerrar</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
