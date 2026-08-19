import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Bell, CalendarClock, FileText, Newspaper, Receipt } from 'lucide-react';
import { dashboardApi } from '../api';
import { useAuthStore, esResponsable } from '../store/authStore';
import type { DashboardEmpleadoDto } from '../types';
import { formatFecha } from '../utils';

export default function DashboardPage() {
  const usuario = useAuthStore((s) => s.usuario);
  const [datos, setDatos] = useState<DashboardEmpleadoDto | null>(null);

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