import api, { apiBaseUrl } from './axios';
import type { Usuario } from '../store/authStore';
import type {
  AnuncioDto,
  AreaDto,
  AsistenciaAreaDto,
  CertificadoDto,
  CertificadoCvDto,
  CertificadoCvAdminDto,
  ExperienciaCvDto,
  AntecedenteAcademicoDto,
  ConsumoTipoLicenciaDto,
  DashboardEmpleadoDto,
  DashboardEmpleadorDto,
  DescargaReciboDto,
EmpleadoDto,
  PaginaEmpleadosDto,
  MisFichadasDto,
  MarcaManualDto,
  HomeOfficeEmpleadoDto,
  NotificacionDto,
  OrganigramaNodoDto,
  PeriodoDto,
  ReciboDisponibleDto,
  RelacionACargoDto,
  SincronizarPeriodosResultDto,
  SolicitudDetalleDto,
  SolicitudLicenciaDto,
  TipoLicenciaDto
} from '../types';

export const authApi = {
  me: () => api.get<Usuario>('/auth/me').then((r) => r.data),
  devLogin: (correo: string) => api.post('/auth/dev-login', { correo }).then((r) => r.data),
  googleDisponible: () => api.get('/auth/google-disponible').then((r) => r.data as { disponible: boolean }),
  loginUrl: () => `${apiBaseUrl}/auth/login`
};

export const recibosApi = {
  disponibles: () => api.get<ReciboDisponibleDto[]>('/recibos/disponibles').then((r) => r.data),
  historial: () => api.get<DescargaReciboDto[]>('/recibos/historial').then((r) => r.data),
  descargar: async (periodoId: string) => {
    const r = await api.post(`/recibos/${periodoId}/descargar`, null, { responseType: 'blob' });
    return r.data as Blob;
  },
  enviarEmail: (periodoId: string) => api.post(`/recibos/${periodoId}/enviar-email`).then((r) => r.data)
};

export const licenciasApi = {
  tipos: () => api.get<TipoLicenciaDto[]>('/licencias/tipos').then((r) => r.data),
  consumo: (anio: number, mes: number) =>
    api.get<ConsumoTipoLicenciaDto[]>('/licencias/consumo', { params: { anio, mes } }).then((r) => r.data),
  misSolicitudes: () => api.get<SolicitudLicenciaDto[]>('/licencias/mis-solicitudes').then((r) => r.data),
  detalle: (id: string) => api.get<SolicitudDetalleDto>(`/licencias/${id}`).then((r) => r.data),
  solicitar: (data: { tipoLicenciaId: string; fechaInicio: string; fechaFin: string; asunto?: string; motivo?: string; adjuntoId?: string | null }) =>
    api.post('/licencias', data).then((r) => r.data),
  cancelar: (id: string) => api.post(`/licencias/${id}/cancelar`).then((r) => r.data),
  pendientes: () => api.get<SolicitudLicenciaDto[]>('/licencias/pendientes').then((r) => r.data),
  pendientesGlobales: () => api.get<SolicitudLicenciaDto[]>('/licencias/pendientes-globales').then((r) => r.data),
  decidir: (id: string, aprobado: boolean, comentario?: string) =>
    api.post(`/licencias/${id}/decidir`, { aprobado, comentario }).then((r) => r.data)
};

export const fichadasApi = {
  mias: (anio: number, mes: number) =>
    api.get<MisFichadasDto>('/fichadas/mias', { params: { anio, mes } }).then((r) => r.data),
  exportar: (anio: number, mes: number, formato: 'pdf' | 'xlsx') =>
    api.get<Blob>('/fichadas/mias/exportar', { params: { anio, mes, formato }, responseType: 'blob' }).then((r) => r.data),
  asistencia: (desde: string, hasta: string, areaId?: string) =>
    api.get<AsistenciaAreaDto>('/fichadas/asistencia', { params: { desde, hasta, areaId } }).then((r) => r.data),
  marcasManuales: (params?: { desde?: string; hasta?: string; empleadoId?: string }) =>
    api.get<MarcaManualDto[]>('/fichadas/marcas-manuales', { params }).then((r) => r.data),
  crearMarcaManual: (data: { empleadoId?: string | null; fechaHora: string; tipo: 'entrada' | 'salida' }) =>
    api.post('/fichadas/marcas-manuales', data).then((r) => r.data),
  editarMarcaManual: (id: string, data: { fechaHora: string; tipo: 'entrada' | 'salida' }) =>
    api.put(`/fichadas/marcas-manuales/${id}`, data).then((r) => r.data),
  eliminarMarcaManual: (id: string) => api.delete(`/fichadas/marcas-manuales/${id}`),
  empleadosHomeOffice: () =>
    api.get<HomeOfficeEmpleadoDto[]>('/fichadas/marcas-manuales/empleados').then((r) => r.data)
};

export const anunciosApi = {
  feed: () => api.get<AnuncioDto[]>('/anuncios/feed').then((r) => r.data),
  marcarLeido: (id: string) => api.post(`/anuncios/${id}/leido`).then((r) => r.data),
  admin: () => api.get<AnuncioDto[]>('/anuncios').then((r) => r.data),
  crear: (data: Partial<AnuncioDto> & { titulo: string; cuerpo: string }) =>
    api.post('/anuncios', data).then((r) => r.data),
  actualizar: (id: string, data: Partial<AnuncioDto>) => api.put(`/anuncios/${id}`, data).then((r) => r.data),
  setActivo: (id: string, activo: boolean) => api.patch(`/anuncios/${id}/activo`, { activo }).then((r) => r.data)
};

export const notificacionesApi = {
  mias: () => api.get<NotificacionDto[]>('/notificaciones').then((r) => r.data),
  noLeidasCount: () => api.get<number>('/notificaciones/no-leidas-count').then((r) => r.data),
  marcarLeida: (id: string) => api.post(`/notificaciones/${id}/leida`).then((r) => r.data)
};

export const certificadosApi = {
  mios: () => api.get<CertificadoDto[]>('/certificados/mios').then((r) => r.data),
  solicitar: (data: { tipo: string; desde: string; hasta: string; destino?: string }) =>
    api.post('/certificados', data).then((r) => r.data),
  descargar: async (id: string) => {
    const r = await api.post(`/certificados/${id}/descargar`, null, { responseType: 'blob' });
    return r.data as Blob;
  }
};

export const cvApi = {
  mios: () => api.get<CertificadoCvDto[]>('/cv-certificados/mios').then((r) => r.data),
  descargarCv: () =>
    api.get('/cv-certificados/mi-cv', { responseType: 'blob' }).then((r) => r.data as Blob),
  observaciones: () =>
    api.get<{ observaciones: string | null }>('/cv-certificados/observaciones').then((r) => r.data.observaciones),
  guardarObservaciones: (texto: string | null) =>
    api.put('/cv-certificados/observaciones', { observaciones: texto }).then((r) => r.data.observaciones as string | null),
  telefono: () =>
    api.get<{ telefono: string | null }>('/cv-certificados/telefono').then((r) => r.data.telefono),
  guardarTelefono: (texto: string | null) =>
    api.put('/cv-certificados/telefono', { telefono: texto }).then((r) => r.data.telefono as string | null),
  experiencias: () =>
    api.get<ExperienciaCvDto[]>('/cv-certificados/experiencias').then((r) => r.data),
  crearExperiencia: (data: Omit<ExperienciaCvDto, 'id'>) =>
    api.post('/cv-certificados/experiencias', data).then((r) => r.data as ExperienciaCvDto),
  editarExperiencia: (id: string, data: Omit<ExperienciaCvDto, 'id'>) =>
    api.put(`/cv-certificados/experiencias/${id}`, data),
  eliminarExperiencia: (id: string) => api.delete(`/cv-certificados/experiencias/${id}`),
  antecedentes: () =>
    api.get<AntecedenteAcademicoDto[]>('/cv-certificados/antecedentes').then((r) => r.data),
  crearAntecedente: (data: { titulo: string; institucion: string; nivel: string; descripcion?: string | null; fechaDesde: string; fechaHasta?: string | null; archivo: File }) => {
    const form = new FormData();
    form.append('titulo', data.titulo);
    form.append('institucion', data.institucion);
    form.append('nivel', data.nivel);
    if (data.descripcion) form.append('descripcion', data.descripcion);
    form.append('fechaDesde', data.fechaDesde);
    if (data.fechaHasta) form.append('fechaHasta', data.fechaHasta);
    form.append('archivo', data.archivo);
    return api.post('/cv-certificados/antecedentes', form).then((r) => r.data as AntecedenteAcademicoDto);
  },
  editarAntecedente: (id: string, data: { titulo: string; institucion: string; nivel: string; descripcion?: string | null; fechaDesde: string; fechaHasta?: string | null }) =>
    api.put(`/cv-certificados/antecedentes/${id}`, data),
  eliminarAntecedente: (id: string) => api.delete(`/cv-certificados/antecedentes/${id}`),
  descargarAntecedente: async (id: string) => {
    const r = await api.get(`/cv-certificados/antecedentes/${id}/archivo`, { responseType: 'blob' });
    return { blob: r.data as Blob, disposition: r.headers['content-disposition'] as string | undefined };
  },
  subir: (data: { nombre: string; institucion: string; tipo: string; fechaObtencion: string; archivo: File }) => {
    const form = new FormData();
    form.append('nombre', data.nombre);
    form.append('institucion', data.institucion);
    form.append('tipo', data.tipo);
    form.append('fechaObtencion', data.fechaObtencion);
    form.append('archivo', data.archivo);
    return api.post('/cv-certificados', form).then((r) => r.data as CertificadoCvDto);
  },
  descargarArchivo: async (id: string) => {
    const r = await api.get(`/cv-certificados/${id}/archivo`, { responseType: 'blob' });
    return { blob: r.data as Blob, disposition: r.headers['content-disposition'] as string | undefined };
  },
  admin: (estado?: string) =>
    api.get<CertificadoCvAdminDto[]>('/cv-certificados/admin', { params: estado ? { estado } : {} }).then((r) => r.data),
  revisar: (id: string, verificado: boolean, comentario?: string) =>
    api.patch(`/cv-certificados/${id}/revisar`, { verificado, comentario }).then((r) => r.data),
  aprobarTodos: () => api.post('/cv-certificados/admin/aprobar-todos').then((r) => r.data as { aprobados: number })
};

export const adjuntosApi = {
  subir: async (archivo: File) => {
    const form = new FormData();
    form.append('archivo', archivo);
    const r = await api.post('/adjuntos', form);
    return r.data as { id: string; nombreArchivo: string };
  }
};

export const adminApi = {
  empleados: (opts?: { texto?: string; pagina?: number; tamano?: number; orden?: string; descendente?: boolean }) =>
    api.get<PaginaEmpleadosDto>('/admin/empleados', { params: opts }).then((r) => r.data),
  empleado: (id: string) => api.get<EmpleadoDto>(`/admin/empleados/${id}`).then((r) => r.data),
  crearEmpleado: (data: { legajo: number; nombre: string; apellido: string; dni?: string; cuil?: string; correo: string; areaId?: string | null; roles: string[] }) =>
    api.post('/admin/empleados', data).then((r) => r.data),
  actualizarEmpleado: (id: string, data: { nombre: string; apellido: string; dni?: string; cuil?: string; areaId?: string | null }) =>
    api.put(`/admin/empleados/${id}`, data).then((r) => r.data),
  setActivoEmpleado: (id: string, activo: boolean) => api.patch(`/admin/empleados/${id}/activo`, { activo }),
  setRoles: (id: string, roles: string[]) => api.put(`/admin/empleados/${id}/roles`, { roles }),
  exportarEmpleados: (formato: 'xlsx' | 'pdf', opts?: { texto?: string; orden?: string; descendente?: boolean }) =>
    api.get<Blob>('/admin/empleados/exportar', { params: { formato, ...opts }, responseType: 'blob' }).then((r) => r.data),

  reporteFichadas: (tipo: string, legajo: number | null, desde: string, hasta: string, formato: 'pdf' | 'xlsx') =>
    api.get<Blob>('/admin/reportes/fichadas', { params: { tipo, legajo, desde, hasta, formato }, responseType: 'blob' }).then((r) => r.data),

  areas: () => api.get<AreaDto[]>('/admin/areas').then((r) => r.data),
  crearArea: (data: { nombre: string; codigo: string; areaPadreId?: string | null }) =>
    api.post('/admin/areas', data).then((r) => r.data),
  actualizarArea: (id: string, data: { nombre: string; codigo: string; areaPadreId?: string | null; activa?: boolean }) =>
    api.put(`/admin/areas/${id}`, data).then((r) => r.data),

  periodos: () => api.get<PeriodoDto[]>('/admin/periodos').then((r) => r.data),
  crearPeriodo: (data: { codigo: string; descripcion?: string; nroLiq?: number | null }) =>
    api.post('/admin/periodos', data).then((r) => r.data),
  actualizarPeriodo: (id: string, data: { descripcion?: string; activo: boolean; nroLiq?: number | null }) =>
    api.put(`/admin/periodos/${id}`, data).then((r) => r.data),
  sincronizarPeriodos: () =>
    api.post<SincronizarPeriodosResultDto>('/admin/periodos/sincronizar').then((r) => r.data),

  tiposLicencia: () => api.get<TipoLicenciaDto[]>('/admin/tipos-licencia').then((r) => r.data),
  crearTipoLicencia: (data: { nombre: string; limiteMensual?: number | null; limiteAnual?: number | null; descripcion?: string; requiereAdjunto: boolean; niveles: string[] }) =>
    api.post('/admin/tipos-licencia', data).then((r) => r.data),
  actualizarTipoLicencia: (id: string, data: { nombre: string; limiteMensual?: number | null; limiteAnual?: number | null; descripcion?: string; requiereAdjunto: boolean }) =>
    api.put(`/admin/tipos-licencia/${id}`, data).then((r) => r.data),
  setActivoTipo: (id: string, activo: boolean) => api.patch(`/admin/tipos-licencia/${id}/activo`, { activo }),
  agregarNivel: (id: string, rolRequerido: string) => api.post(`/admin/tipos-licencia/${id}/niveles`, { rolRequerido }).then((r) => r.data),
  quitarNivel: (nivelId: string) => api.delete(`/admin/tipos-licencia/niveles/${nivelId}`).then((r) => r.data),
  reordenarNiveles: (id: string, nivelIds: string[]) => api.put(`/admin/tipos-licencia/${id}/niveles/orden`, { nivelIds }),

  aCargo: (responsableId: string) => api.get<RelacionACargoDto[]>(`/admin/relaciones/a-cargo/${responsableId}`).then((r) => r.data),
  organigrama: () => api.get<OrganigramaNodoDto[]>('/admin/relaciones/organigrama').then((r) => r.data),
  asignarResponsable: (responsableId: string, empleadoId: string, autorizaMarcas: boolean) =>
    api.post('/admin/relaciones', { responsableId, empleadoId, autorizaMarcas }).then((r) => r.data),
  quitarRelacion: (relacionId: string) => api.delete(`/admin/relaciones/${relacionId}`).then((r) => r.data),

  estadisticasAccesos: (desde: string, hasta: string) =>
    api.get('/estadisticas/accesos', { params: { desde, hasta } }).then((r) => r.data),
  estadisticasFichadas: (desde: string, hasta: string) =>
    api.get('/estadisticas/fichadas', { params: { desde, hasta } }).then((r) => r.data),
  estadisticasGenerales: () => api.get('/estadisticas/generales').then((r) => r.data)
};

export const dashboardApi = {
  empleado: () => api.get<DashboardEmpleadoDto>('/dashboard/empleado').then((r) => r.data),
  empleador: () => api.get<DashboardEmpleadorDto>('/dashboard/empleador').then((r) => r.data)
};