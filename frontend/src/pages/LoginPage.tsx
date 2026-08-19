import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { authApi } from '../api';
import { useAuthStore } from '../store/authStore';

export default function LoginPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const token = useAuthStore((s) => s.token);
  const setToken = useAuthStore((s) => s.setToken);
  const [correo, setCorreo] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [devActivo, setDevActivo] = useState(false);
  const [cargando, setCargando] = useState(false);
  const [googleDisponible, setGoogleDisponible] = useState<boolean | null>(null);

  useEffect(() => {
    authApi.googleDisponible().then((r) => setGoogleDisponible(r.disponible)).catch(() => setGoogleDisponible(false));
  }, []);

  useEffect(() => {
    const err = searchParams.get('error');
    if (err) setError(decodeURIComponent(err));
  }, [searchParams]);

  useEffect(() => {
    if (token) navigate('/', { replace: true });
  }, [token, navigate]);

  const entrarConGoogle = () => {
    window.location.href = authApi.loginUrl();
  };

  const devLogin = async () => {
    setCargando(true);
    setError(null);
    try {
      const sesion = await authApi.devLogin(correo);
      setToken(sesion.token);
      navigate('/', { replace: true });
    } catch (e: any) {
      setError(e.response?.data?.error ?? 'No se pudo iniciar sesión.');
    } finally {
      setCargando(false);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-base p-4">
      <div className="w-full max-w-md">
        <div className="mb-8 text-center">
          <h1 className="text-3xl font-medium text-ink-primary">Portal del Empleado</h1>
          <p className="mt-1 text-sm text-ink-secondary">IUPA – Instituto Universitario Patagónico de las Artes</p>
        </div>

        <div className="card space-y-4">
          {googleDisponible === false && (
            <p className="rounded-lg tint-warning px-3 py-2 text-sm">
              El inicio con Google no está configurado en este entorno. Usá el login de desarrollo.
            </p>
          )}
          {googleDisponible !== false && (
            <button onClick={entrarConGoogle} className="btn-primary w-full">
              Ingresar con Google (@iupa.edu.ar)
            </button>
          )}

          {error && <p className="rounded-lg tint-danger px-3 py-2 text-sm">{error}</p>}

          <div className="relative py-1 text-center text-xs text-ink-muted">
            <span className="bg-surface px-2">o</span>
          </div>

          <div>
            <button
              onClick={() => setDevActivo((v) => !v)}
              className="text-xs text-ink-secondary underline-offset-2 hover:underline"
            >
              {devActivo ? 'Ocultar login de desarrollo' : 'Login de desarrollo (RRHH)'}
            </button>
            {devActivo && (
              <div className="mt-3 space-y-3">
                <select value={correo} onChange={(e) => setCorreo(e.target.value)} className="input">
                  <option value="">Elegí un usuario de prueba...</option>
                  <option value="ana.garcia@iupa.edu.ar">Ana García (Empleado)</option>
                  <option value="carlos.perez@iupa.edu.ar">Carlos Pérez (Empleado)</option>
                  <option value="maria.torres@iupa.edu.ar">María Torres (Empleado)</option>
                  <option value="juan.diaz@iupa.edu.ar">Juan Díaz (Empleado)</option>
                  <option value="martin.lopez@iupa.edu.ar">Martín López (Responsable)</option>
                  <option value="silvia.ramos@iupa.edu.ar">Silvia Ramos (Dirección)</option>
                  <option value="rrhh@iupa.edu.ar">Laura Fernández (RRHH)</option>
                  <option value="administrador@iupa.edu.ar">Sistema (Administrador)</option>
                </select>
                <button onClick={devLogin} disabled={!correo || cargando} className="btn-secondary w-full">
                  {cargando ? 'Ingresando...' : 'Entrar'}
                </button>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}