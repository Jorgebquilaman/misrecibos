import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { authApi } from '../api';
import { useAuthStore } from '../store/authStore';

export default function AuthCallbackPage() {
  const navigate = useNavigate();
  const setToken = useAuthStore((s) => s.setToken);
  const setUsuario = useAuthStore((s) => s.setUsuario);

  useEffect(() => {
    const token = window.location.hash.replace(/^#token=/, '');
    if (!token) {
      navigate('/login?error=sesion_no_iniciada', { replace: true });
      return;
    }

    setToken(token);
    authApi
      .me()
      .then((usuario) => {
        setUsuario(usuario);
        navigate('/', { replace: true });
      })
      .catch(() => navigate('/login', { replace: true }));
  }, [navigate, setToken, setUsuario]);

  return (
    <div className="flex min-h-screen items-center justify-center bg-base">
      <p className="animate-pulse text-ink-primary">Iniciando sesión...</p>
    </div>
  );
}