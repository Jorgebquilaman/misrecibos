import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Bell, CalendarClock, ChevronDown, ChevronUp, FileText, GraduationCap, Newspaper, Receipt, Clock, Award } from 'lucide-react';
import { PieChart, Pie, Cell, BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, Legend } from 'recharts';
import { dashboardApi } from '../api';
import { useAuthStore, esResponsable } from '../store/authStore';
import type { DashboardEmpleadoDto } from '../types';
import { formatFecha } from '../utils';

export default function DashboardPage() {
  const usuario = useAuthStore((s) => s.usuario);
  const [datos, setDatos] = useState<DashboardEmpleadoDto | null>(null);
  const [detalladoAbierto, setDetalladoAbierto] = useState(true);

  useEffect(() => {
    dashboardApi.empleado().then(setDatos).catch(() => {});
  }, []);

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Hola, {usuario?.nombre ?? usuario?.correo}</h1>
        <p className="text-sm text-ink-secondary">Resumen de tu actividad en el portal.</p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Link to="/recibos" className="card transition-shadow hover:shadow-md">
          <Receipt className="mb-2 text-accent-text" />
          <p className="text-sm text-ink-secondary">Último recibo</p>
          <p className="text-lg font-semibold">{datos?.ultimoRecibo ? `${datos.ultimoRecibo.periodoCodigo} · ${datos.ultimoRecibo.yaDescargado ? 'descargado' : 'pendiente'}` : 'Sin recibos'}</p>
        </Link>
        <Link to="/licencias" className="card transition-shadow hover:shadow-md">
          <CalendarClock className="mb-2 text-accent-text" />
          <p className="text-sm text-ink-secondary">Licencias en espera</p>
          <p className="text-lg font-semibold">{datos?.licenciasPendientes ?? 0}</p>
        </Link>
        <Link to="/anuncios" className="card transition-shadow hover:shadow-md">
          <Newspaper className="mb-2 text-accent-text" />
          <p className="text-sm text-ink-secondary">Notificaciones sin leer</p>
          <p className="text-lg font-semibold">{datos?.notificacionesNoLeidas ?? 0}</p>
        </Link>
        <Link to="/certificados" className="card transition-shadow hover:shadow-md">
          <FileText className="mb-2 text-accent-text" />
          <p className="text-sm text-ink-secondary">Certificados laborales</p>
          <p className="text-lg font-semibold">Solicitar</p>
        </Link>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <div className="card">
          <h2 className="mb-3 font-semibold">Fichadas del mes</h2>
          {datos?.fichadasDelMes ? (
            <ul className="space-y-2 text-sm">
              <li className="flex justify-between">
                <span className="text-ink-secondary">Días trabajados</span>
                <span className="font-medium">{datos.fichadasDelMes.diasTrabajados}</span>
              </li>
              <li className="flex justify-between">
                <span className="text-ink-secondary">Días con anomalía</span>
                <span className="font-medium">{datos.fichadasDelMes.diasConAnomalia}</span>
              </li>
              <li className="flex justify-between">
                <span className="text-ink-secondary">Total horas</span>
                <span className="font-medium">{formatHoras(datos.fichadasDelMes.totalHoras)}</span>
              </li>
              <li className="flex justify-between">
                <span className="text-ink-secondary">Promedio diario</span>
                <span className="font-medium">{formatHoras(datos.fichadasDelMes.promedioHoras)}</span>
              </li>
            </ul>
          ) : (
            <p className="text-sm text-ink-secondary">Sin fichadas en el mes.</p>
          )}
        </div>
      </div>

      <div className="card">
        <h2 className="mb-3 font-semibold">Anuncios</h2>
        {datos?.anuncios?.length ? (
          <ul className="space-y-3">
            {datos.anuncios.map((a) => (
              <li key={a.id} className="rounded-lg border border-soft p-3">
                <p className="font-medium">{a.titulo}</p>
                <p className="mt-1 text-sm text-ink-secondary">{a.cuerpo}</p>
                <p className="mt-2 text-xs text-ink-muted">
                  {a.prioridad === 'Urgente' && <span className="mr-2 font-semibold text-danger">URGENTE</span>}
                  Desde {a.fechaDesde ? formatFecha(a.fechaDesde) : ''}
                  {a.fechaHasta ? ` hasta ${formatFecha(a.fechaHasta)}` : ''}
                </p>
              </li>
            ))}
          </ul>
        ) : (
          <p className="text-sm text-ink-secondary">No hay anuncios vigentes.</p>
        )}
      </div>

      <div className="card">
        <button onClick={() => setDetalladoAbierto(!detalladoAbierto)} className="flex w-full items-center justify-between text-left">
          <h2 className="font-semibold">Mi situación en la universidad</h2>
          {detalladoAbierto ? <ChevronUp size={20} /> : <ChevronDown size={20} />}
        </button>
        {!detalladoAbierto && <p className="text-sm text-ink-secondary">Desplegá para ver recibos, licencias, fichadas, CV y gráficos detallados.</p>}
        {detalladoAbierto && datos && (
          <div className="mt-4 space-y-6">
            {/* Recibos */}
            <div className="grid gap-4 lg:grid-cols-2">
              <div className="rounded-lg border border-soft p-4">
                <h3 className="mb-2 flex items-center gap-2 font-medium"><Receipt size={16} /> Recibos de sueldo</h3>
                <div className="grid grid-cols-3 gap-2 text-center">
                  <div><p className="text-2xl font-bold">{datos.recibos?.totalActivos ?? 0}</p><p className="text-xs text-ink-secondary">Períodos</p></div>
                  <div><p className="text-2xl font-bold text-emerald-600">{datos.recibos?.descargados ?? 0}</p><p className="text-xs text-ink-secondary">Descargados</p></div>
                  <div><p className="text-2xl font-bold text-amber-600">{datos.recibos?.pendientes ?? 0}</p><p className="text-xs text-ink-secondary">Pendientes</p></div>
                </div>
                {datos.recibos && datos.recibos.totalActivos > 0 && (
                  <div className="mt-3 h-40">
                    <ResponsiveContainer width="100%" height="100%">
                      <PieChart>
                        <Pie data={[{ name: 'Descargados', value: datos.recibos.descargados }, { name: 'Pendientes', value: datos.recibos.pendientes }]} dataKey="value" cx="50%" cy="50%" outerRadius={60}>
                          <Cell fill="#10b981" /><Cell fill="#f59e0b" />
                        </Pie>
                        <Tooltip /><Legend />
                      </PieChart>
                    </ResponsiveContainer>
                  </div>
                )}
                <div className="mt-2 text-xs text-ink-secondary">
                  {datos.recibos?.disponibles?.slice(0, 3).map((r) => (
                    <div key={r.periodoId} className="flex justify-between"><span>{r.periodoCodigo}</span><span className={r.yaDescargado ? 'text-emerald-600' : 'text-amber-600'}>{r.yaDescargado ? '✓' : '○'}</span></div>
                  ))}
                </div>
                <Link to="/recibos" className="mt-2 inline-block text-sm text-accent-text hover:underline">Ver todos los recibos →</Link>
              </div>

              <div className="rounded-lg border border-soft p-4">
                <h3 className="mb-2 flex items-center gap-2 font-medium"><CalendarClock size={16} /> Licencias</h3>
                <div className="grid grid-cols-4 gap-2 text-center">
                  <div><p className="text-xl font-bold text-amber-600">{datos.licenciasDetallado?.enEspera ?? datos.licenciasPendientes}</p><p className="text-xs text-ink-secondary">En espera</p></div>
                  <div><p className="text-xl font-bold text-emerald-600">{datos.licenciasDetallado?.aprobadas ?? datos.licenciasAprobadas}</p><p className="text-xs text-ink-secondary">Aprobadas</p></div>
                  <div><p className="text-xl font-bold text-red-600">{datos.licenciasDetallado?.desaprobadas ?? datos.licenciasRechazadas}</p><p className="text-xs text-ink-secondary">Rechazadas</p></div>
                  <div><p className="text-xl font-bold">{datos.licenciasDetallado?.canceladas ?? 0}</p><p className="text-xs text-ink-secondary">Canceladas</p></div>
                </div>
                {datos.licenciasDetallado && (datos.licenciasDetallado.enEspera + datos.licenciasDetallado.aprobadas + datos.licenciasDetallado.desaprobadas + datos.licenciasDetallado.canceladas) > 0 && (
                  <div className="mt-3 h-40">
                    <ResponsiveContainer width="100%" height="100%">
                      <PieChart>
                        <Pie data={[
                          { name: 'En espera', value: datos.licenciasDetallado.enEspera },
                          { name: 'Aprobadas', value: datos.licenciasDetallado.aprobadas },
                          { name: 'Rechazadas', value: datos.licenciasDetallado.desaprobadas },
                          { name: 'Canceladas', value: datos.licenciasDetallado.canceladas },
                        ].filter((d) => d.value > 0)} dataKey="value" cx="50%" cy="50%" outerRadius={60}>
                          <Cell fill="#f59e0b" /><Cell fill="#10b981" /><Cell fill="#ef4444" /><Cell fill="#9ca3af" />
                        </Pie>
                        <Tooltip /><Legend />
                      </PieChart>
                    </ResponsiveContainer>
                  </div>
                )}
                {datos.licenciasDetallado?.consumo && datos.licenciasDetallado.consumo.length > 0 && (
                  <div className="mt-3">
                    <p className="text-xs font-medium text-ink-secondary">Consumo del mes</p>
                    <div className="mt-1 space-y-1">
                      {datos.licenciasDetallado.consumo.slice(0, 3).map((c) => (
                        <div key={c.tipoLicenciaId} className="flex justify-between text-xs"><span>{c.tipoLicenciaNombre}</span><span>{c.consumidosMes}{c.limiteMensual ? `/${c.limiteMensual}` : ''} días</span></div>
                      ))}
                    </div>
                  </div>
                )}
                <Link to="/licencias" className="mt-2 inline-block text-sm text-accent-text hover:underline">Gestionar licencias →</Link>
              </div>
            </div>

            {/* Fichadas */}
            <div className="rounded-lg border border-soft p-4">
              <h3 className="mb-2 flex items-center gap-2 font-medium"><Clock size={16} /> Marcas del reloj — este mes</h3>
              {datos.fichadasDetallado ? (
                <>
                  <div className="grid grid-cols-2 gap-2 text-center sm:grid-cols-4">
                    <div><p className="text-xl font-bold">{datos.fichadasDetallado.diasTrabajados}</p><p className="text-xs text-ink-secondary">Días trabajados</p></div>
                    <div><p className="text-xl font-bold text-amber-600">{datos.fichadasDetallado.faltas}</p><p className="text-xs text-ink-secondary">Faltas</p></div>
                    <div><p className="text-xl font-bold text-red-600">{datos.fichadasDetallado.tardanzas}</p><p className="text-xs text-ink-secondary">Tardanzas (&gt;08:30)</p></div>
                    <div><p className="text-xl font-bold">{formatHoras(datos.fichadasDetallado.totalHoras)}</p><p className="text-xs text-ink-secondary">Horas totales</p></div>
                  </div>
                  <div className="mt-2 text-center text-xs text-ink-secondary">Anomalías: {datos.fichadasDetallado.diasConAnomalia} · Promedio: {formatHoras(datos.fichadasDetallado.promedioHoras)}</div>
                  {datos.fichadasDetallado.jornadas.length > 0 && (
                    <div className="mt-3 h-48">
                      <ResponsiveContainer width="100%" height="100%">
                        <BarChart data={datos.fichadasDetallado.jornadas.slice(0, 15).map((j) => ({ fecha: `${j.fecha.slice(8, 10)}/${j.fecha.slice(5, 7)}`, horas: j.horas ? Number(j.horas.split(':')[0]) + Number(j.horas.split(':')[1] ?? 0) / 60 : 0 }))}>
                          <XAxis dataKey="fecha" fontSize={10} /><YAxis fontSize={10} /><Tooltip /><Bar dataKey="horas" fill="var(--color-accent, #0f766e)" radius={[4, 4, 0, 0]} />
                        </BarChart>
                      </ResponsiveContainer>
                    </div>
                  )}
                  <table className="mt-3 w-full text-xs">
                    <thead><tr className="border-b text-ink-secondary"><th className="py-1 text-left">Fecha</th><th>Entrada</th><th>Salida</th><th>Horas</th><th></th></tr></thead>
                    <tbody>
                      {datos.fichadasDetallado.jornadas.slice(0, 8).map((j) => (
                        <tr key={j.fecha} className="border-b border-soft"><td className="py-1">{`${j.fecha.slice(8, 10)}/${j.fecha.slice(5, 7)}`}</td><td className="text-center">{j.entrada ? j.entrada.slice(11, 16) : '—'}</td><td className="text-center">{j.salida ? j.salida.slice(11, 16) : '—'}</td><td className="text-center">{j.horas ?? '—'}</td><td className="text-center">{j.esAnomalia && <span className="rounded bg-amber-100 px-1 text-amber-700">!</span>}</td></tr>
                      ))}
                    </tbody>
                  </table>
                  <Link to="/fichadas" className="mt-2 inline-block text-sm text-accent-text hover:underline">Ver detalle completo →</Link>
                </>
              ) : (
                <p className="text-sm text-ink-secondary">Sin fichadas en el mes.</p>
              )}
            </div>

            {/* CV + Certificados */}
            <div className="grid gap-4 lg:grid-cols-2">
              <div className="rounded-lg border border-soft p-4">
                <h3 className="mb-2 flex items-center gap-2 font-medium"><GraduationCap size={16} /> Mi CV</h3>
                {datos.cv ? (
                  <>
                    <div className="flex items-center gap-4">
                      <div className="relative h-24 w-24">
                        <ResponsiveContainer width="100%" height="100%">
                          <PieChart>
                            <Pie data={[{ value: datos.cv.completitudPct }, { value: 100 - datos.cv.completitudPct }]} dataKey="value" innerRadius={30} outerRadius={45} startAngle={90} endAngle={-270}>
                              <Cell fill="#0f766e" /><Cell fill="#e5e7eb" />
                            </Pie>
                          </PieChart>
                        </ResponsiveContainer>
                        <span className="absolute inset-0 flex items-center justify-center text-sm font-bold">{Math.round(datos.cv.completitudPct)}%</span>
                      </div>
                      <div className="space-y-1 text-sm">
                        <p>Experiencias: <b>{datos.cv.experiencias}</b> · Antecedentes: <b>{datos.cv.antecedentes}</b></p>
                        <p>Certificados: <b>{datos.cv.certificados}</b> ({datos.cv.certificadosVerificados} verificados)</p>
                      </div>
                    </div>
                    {datos.cv.certificadosPorTipo.length > 0 && (
                      <div className="mt-3 h-36">
                        <ResponsiveContainer width="100%" height="100%">
                          <BarChart data={datos.cv.certificadosPorTipo} layout="vertical">
                            <XAxis type="number" fontSize={10} /><YAxis dataKey="tipo" type="category" fontSize={10} width={80} /><Tooltip /><Bar dataKey="cantidad" fill="#0f766e" radius={[0, 4, 4, 0]} />
                          </BarChart>
                        </ResponsiveContainer>
                      </div>
                    )}
                    <Link to="/cv" className="mt-2 inline-block text-sm text-accent-text hover:underline">Editar CV →</Link>
                  </>
                ) : (
                  <p className="text-sm text-ink-secondary">Sin datos de CV.</p>
                )}
              </div>

              <div className="rounded-lg border border-soft p-4">
                <h3 className="mb-2 flex items-center gap-2 font-medium"><Award size={16} /> Certificaciones laborales</h3>
                {datos.certificadosResumen ? (
                  <>
                    <div className="grid grid-cols-2 gap-2 text-center">
                      <div><p className="text-2xl font-bold">{datos.certificadosResumen.total}</p><p className="text-xs text-ink-secondary">Solicitados</p></div>
                      <div><p className="text-2xl font-bold text-emerald-600">{datos.certificadosResumen.generados}</p><p className="text-xs text-ink-secondary">Generados</p></div>
                    </div>
                    <div className="mt-3 h-32">
                      <ResponsiveContainer width="100%" height="100%">
                        <PieChart>
                          <Pie data={[{ name: 'Generados', value: datos.certificadosResumen.generados }, { name: 'Pendientes', value: Math.max(0, datos.certificadosResumen.total - datos.certificadosResumen.generados) }]} dataKey="value" cx="50%" cy="50%" outerRadius={50}>
                            <Cell fill="#10b981" /><Cell fill="#e5e7eb" />
                          </Pie>
                          <Tooltip /><Legend />
                        </PieChart>
                      </ResponsiveContainer>
                    </div>
                  </>
                ) : (
                  <p className="text-sm text-ink-secondary">Sin certificaciones.</p>
                )}
                <Link to="/certificados" className="mt-2 inline-block text-sm text-accent-text hover:underline">Solicitar certificado →</Link>
              </div>
            </div>
          </div>
        )}
      </div>

      {esResponsable(usuario?.roles) && <EmpleadorCard />}
    </div>
  );
}

function formatHoras(t?: string | null) {
  if (!t) return '00:00';
  const partes = t.split('.');
  const dias = partes.length === 2 ? Number(partes[0]) : 0;
  const resto = partes.length === 2 ? partes[1] : partes[0];
  const [h, m] = resto.split(':');
  const horas = Number(h || 0) + (Number.isFinite(dias) ? dias * 24 : 0);
  return `${String(horas).padStart(2, '0')}:${m ?? '00'}`;
}

function EmpleadorCard() {
  const [datos, setDatos] = useState<any>(null);
  useEffect(() => {
    dashboardApi.empleador().then(setDatos).catch(() => {});
  }, []);

  if (!datos) return null;

  return (
    <div className="card">
      <h2 className="mb-3 font-semibold">Vista de empleador</h2>
      <div className="grid gap-4 sm:grid-cols-4">
        <div>
          <p className="text-xs text-ink-secondary">Pendientes de tu aprobación</p>
          <p className="text-xl font-bold">{datos.pendientesDeMiAprobacion}</p>
        </div>
        <div>
          <p className="text-xs text-ink-secondary">Empleados a tu cargo</p>
          <p className="text-xl font-bold">{datos.empleadosACargo}</p>
        </div>
        <div>
          <p className="text-xs text-ink-secondary">Total pendientes (RRHH)</p>
          <p className="text-xl font-bold">{datos.totalPendientes}</p>
        </div>
        <div>
          <p className="text-xs text-ink-secondary">Tiempo promedio de aprobación</p>
          <p className="text-xl font-bold">{datos.tiempoPromedioAprobacion || '—'}</p>
        </div>
      </div>
      {datos.licenciasPorTipoDelMes?.length > 0 && (
        <div className="mt-4 flex items-center gap-2 text-sm text-ink-secondary">
          <Bell size={16} />
          {datos.licenciasPorTipoDelMes
            .map((l: any) => `${l.tipoLicencia}: ${l.cantidad} (${l.dias} días)`)
            .join(' · ')}{' '}
          este mes.
        </div>
      )}
    </div>
  );
}