export interface PeriodoDto {
  id: string;
  codigo: string;
  descripcion: string | null;
  activo: boolean;
  nroLiq: number | null;
}

export interface SincronizarPeriodosResultDto {
  importados: number;
  actualizados: number;
  sinCambios: number;
  conError: number;
  totalExternos: number;
}

export interface ReciboDisponibleDto {
  periodoId: string;
  periodoCodigo: string;
  periodoDescripcion: string | null;
  yaDescargado: boolean;
  fechaUltimaDescarga: string | null;
}

export interface DescargaReciboDto {
  id: string;
  periodoId: string;
  periodoCodigo: string;
  fechaHora: string;
  origen: string;
}

export interface TipoLicenciaDto {
  id: string;
  nombre: string;
  limiteMensual: number | null;
  limiteAnual: number | null;
  descripcion: string | null;
  requiereAdjunto: boolean;
  activo: boolean;
  niveles: { id: string; orden: number; rolRequerido: string }[];
}

export interface ConsumoTipoLicenciaDto {
  tipoLicenciaId: string;
  tipoLicenciaNombre: string;
  consumidosMes: number;
  consumidosAnio: number;
  limiteMensual: number | null;
  limiteAnual: number | null;
}

export interface SolicitudLicenciaDto {
  id: string;
  empleadoId: string;
  empleadoNombre: string;
  tipoLicenciaId: string;
  tipoLicenciaNombre: string;
  fechaInicio: string;
  fechaFin: string;
  dias: number;
  asunto: string | null;
  motivo: string | null;
  adjuntoId: string | null;
  estado: string;
  fechaSolicitud: string;
}

export interface NivelAprobacionDto {
  id: string;
  orden: number;
  rolRequerido: string;
  aprobadorId: string | null;
  aprobadorNombre: string | null;
  aprobado: boolean;
  comentario: string | null;
  fecha: string | null;
}

export interface SolicitudDetalleDto {
  solicitud: SolicitudLicenciaDto;
  niveles: NivelAprobacionDto[];
  nivelPendiente: NivelAprobacionDto | null;
  puedeAprobar: boolean;
  razonBloqueo: string | null;
}

export interface JornadaDto {
  fecha: string;
  entrada: string | null;
  salida: string | null;
  horas: string;
  esAnomalia: boolean;
}

export interface ResumenJornadasDto {
  diasTrabajados: number;
  horasTotales: string;
  diasConAnomalia: number;
}

export interface MisFichadasDto {
  anio: number;
  mes: number;
  jornadas: JornadaDto[];
  resumen: ResumenJornadasDto;
}

export interface AsistenciaAreaDto {
  areaId: string | null;
  areaNombre: string;
  desde: string;
  hasta: string;
  empleados: {
    empleadoId: string;
    empleadoNombre: string;
    legajo: number;
    area: string | null;
    diasTrabajados: number;
    diasConAnomalia: number;
  }[];
}

export interface AnuncioDto {
  id: string;
  titulo: string;
  cuerpo: string;
  prioridad: string;
  tipo: string;
  fechaDesde: string | null;
  fechaHasta: string | null;
  alcance: string;
  activo: boolean;
  leido: boolean;
  fechaCreacion: string;
}

export interface MarcaManualDto {
  id: string;
  empleadoId: string;
  fechaHora: string;
  tipo: 'entrada' | 'salida';
  origen: string;
}

export interface HomeOfficeEmpleadoDto {
  id: string;
  nombre: string;
  apellido: string;
  legajo: number;
}

export interface NotificacionDto {
  id: string;
  titulo: string;
  cuerpo: string;
  link: string | null;
  leida: boolean;
  fechaHora: string;
}

export interface CertificadoDto {
  id: string;
  tipo: string;
  desde: string;
  hasta: string;
  destino: string | null;
  estado: string;
  archivoAdjuntoId: string | null;
  fechaSolicitud: string;
}

export interface CertificadoCvDto {
  id: string;
  nombre: string;
  institucion: string;
  tipo: string;
  fechaObtencion: string;
  estado: string;
  comentarioRevision: string | null;
  adjuntoId: string;
  nombreArchivo: string;
  tamanoBytes: number;
  fechaCarga: string;
}

export interface CertificadoCvAdminDto extends CertificadoCvDto {
  empleadoId: string;
  legajo: number;
  empleadoNombre: string;
  area: string | null;
}

export interface ExperienciaCvDto {
  id?: string;
  puesto: string;
  institucion: string;
  descripcion: string | null;
  fechaDesde: string;
  fechaHasta: string | null;
}

export interface AntecedenteAcademicoDto {
  id: string;
  titulo: string;
  institucion: string;
  nivel: string;
  descripcion: string | null;
  fechaDesde: string;
  fechaHasta: string | null;
  adjuntoId: string;
  nombreArchivo: string;
  tamanoBytes: number;
  fechaCarga: string;
}

export interface AreaDto {
  id: string;
  nombre: string;
  codigo: string;
  areaPadreId: string | null;
  activa: boolean;
}

export interface EmpleadoDto {
  id: string;
  legajo: number;
  nombre: string;
  apellido: string;
  dni: string | null;
  cuil: string | null;
  correo: string;
  areaId: string | null;
  areaNombre: string | null;
  roles: string[];
  activo: boolean;
}

export interface PaginaEmpleadosDto {
  items: EmpleadoDto[];
  total: number;
  pagina: number;
  tamano: number;
}

export interface RelacionACargoDto {
  id: string;
  responsableId: string;
  responsableNombre: string;
  empleadoId: string;
  empleadoNombre: string;
  autorizaMarcas: boolean;
}

export interface OrganigramaNodoDto {
  id: string;
  nombre: string;
  codigo: string;
  hijas: OrganigramaNodoDto[];
  empleados: EmpleadoDto[];
}

export interface DashboardEmpleadoDto {
  ultimoRecibo: { periodoId: string; periodoCodigo: string; periodoDescripcion: string | null; yaDescargado: boolean; fechaUltimaDescarga: string | null } | null;
  licenciasPendientes: number;
  licenciasAprobadas: number;
  licenciasRechazadas: number;
  fichadasDelMes: { diasTrabajados: number; diasConAnomalia: number; totalHoras: string; promedioHoras: string | null } | null;
  anuncios: AnuncioDto[];
  notificacionesNoLeidas: number;
  recibos?: { totalActivos: number; descargados: number; pendientes: number; disponibles: { periodoId: string; periodoCodigo: string; periodoDescripcion: string | null; yaDescargado: boolean; fechaUltimaDescarga: string | null }[] } | null;
  licenciasDetallado?: { enEspera: number; aprobadas: number; desaprobadas: number; canceladas: number; consumo: { tipoLicenciaId: string; tipoLicenciaNombre: string; consumidosMes: number; consumidosAnio: number; limiteMensual: number | null; limiteAnual: number | null }[] } | null;
  fichadasDetallado?: { diasTrabajados: number; diasConAnomalia: number; faltas: number; tardanzas: number; totalHoras: string; promedioHoras: string | null; jornadas: { fecha: string; entrada: string | null; salida: string | null; horas: string | null; esAnomalia: boolean }[] } | null;
  cv?: { experiencias: number; antecedentes: number; certificados: number; certificadosVerificados: number; completitudPct: number; certificadosPorTipo: { tipo: string; cantidad: number }[] } | null;
  certificadosResumen?: { total: number; generados: number } | null;
}

export interface DashboardEmpleadorDto {
  pendientesDeMiAprobacion: number;
  empleadosACargo: number;
  totalPendientes: number;
  licenciasPorTipoDelMes: { tipoLicencia: string; cantidad: number; dias: number }[];
  tiempoPromedioAprobacion: string;
}