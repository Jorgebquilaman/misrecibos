export function formatFecha(fecha: string | Date | null | undefined): string {
  if (!fecha) return '';
  const d = typeof fecha === 'string' ? new Date(fecha) : fecha;
  return d.toLocaleDateString('es-AR', { day: '2-digit', month: '2-digit', year: 'numeric' });
}

export function formatFechaHora(fecha: string): string {
  return new Date(fecha).toLocaleString('es-AR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  });
}

export function descargarBlob(blob: Blob, nombre: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = nombre;
  a.click();
  URL.revokeObjectURL(url);
}

export function mesActual(): { anio: number; mes: number } {
  const ahora = new Date();
  return { anio: ahora.getFullYear(), mes: ahora.getMonth() + 1 };
}

export const ETIQUETA_ESTADO: Record<string, string> = {
  EnEspera: 'En espera',
  Aprobada: 'Aprobada',
  Desaprobada: 'Desaprobada',
  Cancelada: 'Cancelada'
};

export const COLOR_ESTADO: Record<string, string> = {
  EnEspera: 'tint-warning',
  Aprobada: 'tint-success',
  Desaprobada: 'tint-danger',
  Cancelada: 'bg-surface-alt text-ink-secondary'
};

export const ETIQUETA_ROL: Record<string, string> = {
  Empleado: 'Empleado',
  Responsable: 'Responsable',
  Rrhh: 'RRHH',
  Administrador: 'Administrador',
  Direccion: 'Dirección',
  HomeOffice: 'Home Office'
};