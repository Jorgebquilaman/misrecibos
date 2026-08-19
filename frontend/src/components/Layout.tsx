import { useEffect, useState } from 'react';
import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import {
  Bell,
  Building2,
  CalendarClock,
  CalendarDays,
  Clock,
  FileText,
  GraduationCap,
  Home,
  LogOut,
  Menu,
  Moon,
  Newspaper,
  PenLine,
  Receipt,
  Sun,
  TableProperties,
  Users,
  X
} from 'lucide-react';
import { useAuthStore, esResponsable, esRrhh, puedeCargarMarcasManuales } from '../store/authStore';
import { notificacionesApi } from '../api';
import { formatFecha } from '../utils';

export default function Layout() {
  const { usuario, logout } = useAuthStore();
  const navigate = useNavigate();
  const [abierto, setAbierto] = useState(false);
  const [noLeidas, setNoLeidas] = useState(0);
  const [tema, setTema] = useState(() => document.documentElement.getAttribute('data-theme') === 'light' ? 'light' : 'dark');

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', tema);
    localStorage.setItem('tema', tema);
    const meta = document.querySelector('meta[name="theme-color"]');
    meta?.setAttribute('content', tema === 'light' ? '#f7f3ee' : '#0f0a0b');
  }, [tema]);

  useEffect(() => {
    const cargar = () => notificacionesApi.noLeidasCount().then(setNoLeidas).catch(() => {});
    cargar();
    const id = setInterval(cargar, 60000);
    return () => clearInterval(id);
  }, []);

  const salir = () => {
    logout();
    navigate('/login');
  };

  const linkClase = ({ isActive }: { isActive: boolean }) =>
    `flex items-center gap-3 rounded-pill px-3 py-2 text-sm font-medium transition-colors ${
      isActive ? 'bg-accent text-accent-ink' : 'text-ink-muted hover:bg-surface-soft hover:text-ink-primary'
    }`;

  return (
    <div className="flex min-h-screen">
      <aside
        className={`fixed inset-y-0 left-0 z-40 flex w-64 transform flex-col border-r border-soft bg-surface transition-transform lg:sticky lg:top-0 lg:h-screen lg:translate-x-0 ${
          abierto ? 'translate-x-0' : '-translate-x-full'
        }`}
      >
        <div className="flex items-center justify-between px-4 py-4">
          <div>
            <img
              src="https://iupa.edu.ar/wp-content/themes/IUPA-NUEVO/img/svg/Logo-IUPA.svg"
              alt="IUPA"
              className={`mb-2 h-9 ${tema === 'light' ? 'invert' : ''}`}
            />
            <p className="font-serif text-lg font-medium text-ink-primary">Portal IUPA</p>
            <p className="text-xs text-ink-muted">Instituto Universitario Patagónico de las Artes</p>
          </div>
          <button className="text-ink-primary lg:hidden" onClick={() => setAbierto(false)}>
            <X size={20} />
          </button>
        </div>

        <nav className="mt-2 flex-1 space-y-1 overflow-y-auto px-3 pb-4">
          <NavLink to="/" className={linkClase} end>
            <Home size={18} /> Inicio
          </NavLink>
          <NavLink to="/recibos" className={linkClase}>
            <Receipt size={18} /> Recibos
          </NavLink>
          <NavLink to="/licencias" className={linkClase}>
            <CalendarClock size={18} /> Licencias
          </NavLink>
          <NavLink to="/fichadas" className={linkClase}>
            <Clock size={18} /> Fichadas
          </NavLink>
          {puedeCargarMarcasManuales(usuario?.roles) && (
            <NavLink to="/marcas-manuales" className={linkClase}>
              <PenLine size={18} /> Marcas manuales
            </NavLink>
          )}
          <NavLink to="/anuncios" className={linkClase}>
            <Newspaper size={18} /> Anuncios
          </NavLink>
          <NavLink to="/certificados" className={linkClase}>
            <FileText size={18} /> Certificados
          </NavLink>
          <NavLink to="/notificaciones" className={linkClase}>
            <Bell size={18} />
            Notificaciones
            {noLeidas > 0 && (
              <span className="ml-auto rounded-pill bg-accent px-2 py-0.5 text-xs text-accent-ink">{noLeidas}</span>
            )}
          </NavLink>

          {esResponsable(usuario?.roles) && (
            <>
              <p className="mt-4 px-3 text-[11px] font-medium uppercase tracking-[0.12em] text-ink-muted">Empleador</p>
              <NavLink to="/pendientes" className={linkClase}>
                <Users size={18} /> Aprobaciones
              </NavLink>
            </>
          )}

          {esRrhh(usuario?.roles) && (
            <>
              <p className="mt-4 px-3 text-[11px] font-medium uppercase tracking-[0.12em] text-ink-muted">Administración</p>
              <NavLink to="/admin/empleados" className={linkClase}>
                <Users size={18} /> Empleados
              </NavLink>
              <NavLink to="/admin/tipos-licencia" className={linkClase}>
                <CalendarClock size={18} /> Tipos de licencia
              </NavLink>
              <NavLink to="/admin/periodos" className={linkClase}>
                <CalendarDays size={18} /> Períodos
              </NavLink>
              <NavLink to="/admin/areas" className={linkClase}>
                <Building2 size={18} /> Áreas y relaciones
              </NavLink>
              <NavLink to="/admin/estadisticas" className={linkClase}>
                <GraduationCap size={18} /> Estadísticas
              </NavLink>
              <NavLink to="/admin/reportes-fichadas" className={linkClase}>
                <TableProperties size={18} /> Reportes de fichadas
              </NavLink>
            </>
          )}
        </nav>

        <div className="border-t border-soft p-4">
          <p className="truncate text-sm font-medium text-ink-primary">{usuario?.nombre ?? usuario?.correo}</p>
          <p className="truncate text-xs text-ink-muted">
            {usuario?.roles.join(' · ')} {usuario?.legajo ? `· Leg. ${usuario.legajo}` : ''}
          </p>
          <div className="mt-3 flex items-center justify-between">
            <button onClick={salir} className="flex items-center gap-2 text-sm text-ink-secondary hover:text-ink-primary">
              <LogOut size={16} /> Cerrar sesión
            </button>
            <button
              onClick={() => setTema((t) => (t === 'light' ? 'dark' : 'light'))}
              className="flex h-8 w-8 items-center justify-center rounded-pill bg-surface-soft text-ink-secondary transition-colors hover:text-ink-primary"
              title={tema === 'light' ? 'Cambiar a tema oscuro' : 'Cambiar a tema claro'}
            >
              {tema === 'light' ? <Moon size={16} /> : <Sun size={16} />}
            </button>
          </div>
        </div>
      </aside>

      {abierto && <div className="fixed inset-0 z-30 bg-black/50 lg:hidden" onClick={() => setAbierto(false)} />}

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-20 flex items-center gap-3 border-b border-soft bg-base/90 px-4 py-3 backdrop-blur lg:hidden">
          <button onClick={() => setAbierto(true)}>
            <Menu size={22} />
          </button>
          <p className="font-serif font-medium">Portal IUPA</p>
          <span className="ml-auto text-xs text-ink-secondary">{formatFecha(new Date())}</span>
        </header>
        <main className="flex-1 p-4 lg:p-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}