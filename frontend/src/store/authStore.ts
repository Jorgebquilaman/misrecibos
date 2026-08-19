import { create } from 'zustand';

export interface Usuario {
  empleadoId: string;
  correo: string;
  nombre: string | null;
  legajo: string | null;
  areaId: string | null;
  roles: string[];
}

interface AuthState {
  token: string | null;
  usuario: Usuario | null;
  setToken: (token: string) => void;
  setUsuario: (usuario: Usuario) => void;
  logout: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  token: localStorage.getItem('token'),
  usuario: null,

  setToken: (token) => {
    localStorage.setItem('token', token);
    set({ token });
  },

  setUsuario: (usuario) => set({ usuario }),

  logout: () => {
    localStorage.removeItem('token');
    set({ token: null, usuario: null });
  }
}));

export function tieneRol(roles: string[] | undefined, ...requeridos: string[]): boolean {
  if (!roles) return false;
  return roles.some((r) => requeridos.includes(r));
}

export function esResponsable(roles: string[] | undefined): boolean {
  return tieneRol(roles, 'Responsable', 'Rrhh', 'Administrador', 'Direccion');
}

export function esRrhh(roles: string[] | undefined): boolean {
  return tieneRol(roles, 'Rrhh', 'Administrador');
}

export function esAdmin(roles: string[] | undefined): boolean {
  return tieneRol(roles, 'Administrador');
}

export function esHomeOffice(roles: string[] | undefined): boolean {
  return tieneRol(roles, 'HomeOffice');
}

export function puedeCargarMarcasManuales(roles: string[] | undefined): boolean {
  return esResponsable(roles) || esHomeOffice(roles);
}