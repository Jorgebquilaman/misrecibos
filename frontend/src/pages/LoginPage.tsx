import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { authApi } from '../api';
import { useAuthStore } from '../store/authStore';

export default function LoginPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const token = useAuthStore((s) => s.token);
  const [error, setError] = useState<string | null>(null);
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

  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-[#f9fafb] p-4">
      {/* Fondo decorativo claro */}
      <div className="pointer-events-none absolute inset-0">
        <div className="absolute -top-32 -right-32 h-[480px] w-[480px] rounded-full bg-[#e2e8f0]/50 blur-3xl" />
        <div className="absolute -bottom-32 -left-32 h-[520px] w-[520px] rounded-full bg-[#cbd5e1]/20 blur-3xl" />
        <div className="absolute left-1/2 top-1/2 h-[600px] w-[800px] -translate-x-1/2 -translate-y-1/2 rounded-full bg-white/80 blur-2xl" />
      </div>

      <div className="relative w-full max-w-[420px]">
        {/* Marca superior */}
        <div className="mb-4 flex justify-center">
          <span className="rounded-full bg-white px-3 py-1 text-[11px] font-medium tracking-wide text-ink-secondary shadow-sm">
            Instituto Universitario Patagónico de las Artes
          </span>
        </div>

        {/* Logo fuera del recuadro, sobre el nombre */}
        <div className="mb-3 flex justify-center">
          <img
            src="https://iupa.edu.ar/wp-content/themes/IUPA-NUEVO/img/svg/Logo-IUPA.svg"
            alt="IUPA"
            className="h-12 w-auto object-contain drop-shadow-sm"
            style={{ filter: 'invert(1) brightness(0.15)' }}
          />
        </div>

        <div className="overflow-hidden rounded-[28px] border border-[#d6c7b8]/40 bg-[#f5ebe0] shadow-xl shadow-black/[0.06]">
          <img src="/login-illustration.png" alt="Sede IUPA - Ilustración otoñal" className="h-44 w-full object-cover" />
          <div className="p-8 text-center">
            <h1 className="font-serif text-2xl font-semibold tracking-tight text-ink-primary">
              Portal del Empleado
            </h1>
            <p className="mt-1.5 text-sm leading-5 text-ink-secondary">
              Accedé con tu cuenta institucional<br />
              <span className="font-medium text-ink-primary">@iupa.edu.ar</span>
            </p>
          </div>

          <div className="mt-7 space-y-4">
            {googleDisponible === false && (
              <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900 dark:border-amber-900/30 dark:bg-amber-950/30 dark:text-amber-200">
                El inicio de sesión no está disponible en este momento. Contactá a RRHH.
              </div>
            )}

            {googleDisponible !== false && (
              <button
                onClick={entrarConGoogle}
                className="flex w-full items-center justify-center gap-3 rounded-full bg-[#0f0a0b] px-5 py-3.5 text-sm font-medium text-white shadow-lg shadow-black/10 transition hover:bg-black hover:shadow-xl active:scale-[0.98]"
              >
                <span className="flex h-7 w-7 items-center justify-center rounded-full bg-white">
                  <svg viewBox="0 0 24 24" className="h-4 w-4" aria-hidden>
                    <path fill="#4285F4" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92a5.06 5.06 0 01-2.2 3.3v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.08z" />
                    <path fill="#34A853" d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" />
                    <path fill="#FBBC05" d="M5.84 14.09A6.97 6.97 0 015.48 12c0-.72.13-1.43.36-2.09V7.07H2.18A11 11 0 001 12c0 1.78.43 3.45 1.18 4.93l3.66-2.84z" />
                    <path fill="#EA4335" d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" />
                  </svg>
                </span>
                Continuar con Google
              </button>
            )}

            {error && (
              <div className="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800 dark:border-red-900/30 dark:bg-red-950/30 dark:text-red-200">
                {error}
              </div>
            )}

            <p className="pt-2 text-center text-xs leading-4 text-ink-muted">
              Al continuar aceptás las políticas internas de uso.<br />
              Si no tenés acceso, solicitá tu cuenta a{' '}
              <span className="font-medium text-ink-secondary">RRHH</span>.
            </p>
          </div>
        </div>

        <p className="mt-6 text-center text-xs text-ink-muted">
          © {new Date().getFullYear()} IUPA · Portal del Empleado
        </p>
      </div>
    </div>
  );
}