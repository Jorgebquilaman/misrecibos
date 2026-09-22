import { useEffect, useState } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { authApi } from '../api';
import { puedeCargarMarcasManuales, esAdmin, esRrhh, useAuthStore } from '../store/authStore';
import Layout from '../components/Layout';
import LoginPage from '../pages/LoginPage';
import AuthCallbackPage from '../pages/AuthCallbackPage';
import DashboardPage from '../pages/DashboardPage';
import RecibosPage from '../pages/RecibosPage';
import LicenciasPage from '../pages/LicenciasPage';
import PendientesPage from '../pages/PendientesPage';
import FichadasPage from '../pages/FichadasPage';
import MarcasManualesPage from '../pages/MarcasManualesPage';
import AnunciosPage from '../pages/AnunciosPage';
import CertificadosPage from '../pages/CertificadosPage';
import MiCvPage from '../pages/MiCvPage';
import NotificacionesPage from '../pages/NotificacionesPage';
import AdminEmpleadosPage from '../pages/admin/AdminEmpleadosPage';
import AdminCertificadosCvPage from '../pages/admin/AdminCertificadosCvPage';
import AdminTiposLicenciaPage from '../pages/admin/AdminTiposLicenciaPage';
import AdminPeriodosPage from '../pages/admin/AdminPeriodosPage';
import AdminAreasPage from '../pages/admin/AdminAreasPage';
import AdminEstadisticasPage from '../pages/admin/AdminEstadisticasPage';
import AdminReportesFichadasPage from '../pages/admin/AdminReportesFichadasPage';
import AdminAnunciosPage from '../pages/admin/AdminAnunciosPage';
import AdminTrazabilidadPage from '../pages/admin/AdminTrazabilidadPage';
import AdminEdificiosPage from '../pages/admin/AdminEdificiosPage';
import AdminRelojesPage from '../pages/admin/AdminRelojesPage';
import AdminReportesPage from '../pages/admin/AdminReportesPage';
import MisReportesPage from '../pages/MisReportesPage';
import AdminDashboardsPage from '../pages/admin/AdminDashboardsPage';
import AdminDescargaRelojPage from '../pages/admin/AdminDescargaRelojPage';
import MisDashboardsPage from '../pages/MisDashboardsPage';

function MarcasManualesProtegida() {
  const roles = useAuthStore((s) => s.usuario?.roles);
  return puedeCargarMarcasManuales(roles) ? <MarcasManualesPage /> : <Navigate to="/" replace />;
}

function SoloAdminProtegida() {
  const roles = useAuthStore((s) => s.usuario?.roles);
  return esAdmin(roles) ? <AdminTrazabilidadPage /> : <Navigate to="/" replace />;
}

function SoloAdminEdificios() {
  const roles = useAuthStore((s) => s.usuario?.roles);
  return esAdmin(roles) ? <AdminEdificiosPage /> : <Navigate to="/" replace />;
}

function SoloAdminRelojes() {
  const roles = useAuthStore((s) => s.usuario?.roles);
  return esAdmin(roles) ? <AdminRelojesPage /> : <Navigate to="/" replace />;
}

function SoloAdminDescargaReloj() {
  const roles = useAuthStore((s) => s.usuario?.roles);
  return esAdmin(roles) ? <AdminDescargaRelojPage /> : <Navigate to="/" replace />;
}

function SoloAdminReportes() {
  const roles = useAuthStore((s) => s.usuario?.roles);
  return esAdmin(roles) ? <AdminReportesPage /> : <Navigate to="/" replace />;
}

function SoloRrhhReportes() {
  const roles = useAuthStore((s) => s.usuario?.roles);
  return esRrhh(roles) ? <MisReportesPage /> : <Navigate to="/" replace />;
}

function SoloAdminDashboards() {
  const roles = useAuthStore((s) => s.usuario?.roles);
  return esAdmin(roles) ? <AdminDashboardsPage /> : <Navigate to="/" replace />;
}

function SoloRrhhDashboards() {
  const roles = useAuthStore((s) => s.usuario?.roles);
  return esRrhh(roles) ? <MisDashboardsPage /> : <Navigate to="/" replace />;
}

export function RouterProvider() {
  const token = useAuthStore((s) => s.token);
  const usuario = useAuthStore((s) => s.usuario);
  const setUsuario = useAuthStore((s) => s.setUsuario);
  const [hidratando, setHidratando] = useState(false);

  useEffect(() => {
    if (!token || usuario) return;
    setHidratando(true);
    authApi
      .me()
      .then(setUsuario)
      .catch(() => {})
      .finally(() => setHidratando(false));
  }, [token, usuario, setUsuario]);

  if (!token) {
    return (
      <Routes>
        <Route path="/auth/callback" element={<AuthCallbackPage />} />
        <Route path="*" element={<LoginPage />} />
      </Routes>
    );
  }

  if (!usuario && hidratando) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-base">
        <p className="animate-pulse text-ink-primary">Cargando...</p>
      </div>
    );
  }

  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/" element={<DashboardPage />} />
        <Route path="/recibos" element={<RecibosPage />} />
        <Route path="/licencias" element={<LicenciasPage />} />
        <Route path="/pendientes" element={<PendientesPage />} />
        <Route path="/fichadas" element={<FichadasPage />} />
        <Route path="/marcas-manuales" element={<MarcasManualesProtegida />} />
        <Route path="/anuncios" element={<AnunciosPage />} />
        <Route path="/certificados" element={<CertificadosPage />} />
        <Route path="/cv" element={<MiCvPage />} />
        <Route path="/notificaciones" element={<NotificacionesPage />} />
        <Route path="/admin/empleados" element={<AdminEmpleadosPage />} />
        <Route path="/admin/cv" element={<AdminCertificadosCvPage />} />
        <Route path="/admin/tipos-licencia" element={<AdminTiposLicenciaPage />} />
        <Route path="/admin/periodos" element={<AdminPeriodosPage />} />
        <Route path="/admin/areas" element={<AdminAreasPage />} />
        <Route path="/admin/estadisticas" element={<AdminEstadisticasPage />} />
        <Route path="/admin/reportes-fichadas" element={<AdminReportesFichadasPage />} />
        <Route path="/admin/anuncios" element={<AdminAnunciosPage />} />
        <Route path="/admin/trazabilidad" element={<SoloAdminProtegida />} />
        <Route path="/admin/edificios" element={<SoloAdminEdificios />} />
        <Route path="/admin/relojes" element={<SoloAdminRelojes />} />
        <Route path="/admin/descarga-reloj" element={<SoloAdminDescargaReloj />} />
        <Route path="/admin/reportes" element={<SoloAdminReportes />} />
        <Route path="/reportes" element={<SoloRrhhReportes />} />
        <Route path="/admin/dashboards" element={<SoloAdminDashboards />} />
        <Route path="/dashboards" element={<SoloRrhhDashboards />} />
      </Route>
      <Route path="/auth/callback" element={<AuthCallbackPage />} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}