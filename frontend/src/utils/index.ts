export function formatFecha(fecha: string | Date | null | undefined): string {
  if (!fecha) return '';
  let d: Date;
  if (typeof fecha === 'string') {
    // DateOnly "YYYY-MM-DD" de la API: parsear como fecha local, no UTC (sino en UTC-3 se ve un día atrás)
    const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(fecha);
    if (m) d = new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
    else d = new Date(fecha);
  } else d = fecha;
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