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
  latitud?: number | null;
  longitud?: number | null;
  edificio?: string | null;
}

export interface EdificioDto {
  id: string;
  nombre: string;
  latitud: number;
  longitud: number;
  radioMetros: number;
  activo: boolean;
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

export interface ExperienciaAdjuntoDto {
  id: string;
  adjuntoId: string;
  nombreArchivo: string;
  contentType: string;
  tamanoBytes: number;
  fechaCarga: string;
}

export type SeccionCvItem = 'antecedentes-prof-artisticos' | 'produccion' | 'otros-antecedentes';

export interface CvItemAdjuntoDto {
  id: string;
  adjuntoId: string;
  nombreArchivo: string;
  contentType: string;
  tamanoBytes: number;
  fechaCarga: string;
}

export interface CvItemDto {
  id: string;
  seccion: SeccionCvItem;
  categoria: string;
  titulo: string;
  institucion: string | null;
  descripcion: string | null;
  fechaDesde: string;
  fechaHasta: string | null;
  adjuntos?: CvItemAdjuntoDto[];
}

export interface ExperienciaCvDto {
  id?: string;
  puesto: string;
  institucion: string;
  descripcion: string | null;
  fechaDesde: string;
  fechaHasta: string | null;
  adjuntos?: ExperienciaAdjuntoDto[];
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

export interface TrazabilidadRegistroDto {
  traceId: string;
  userId: string;
  inputFile: string;
  inputHashSha256: string;
  fechaHora: string;
  encoding: string;
  position: string;
  outputFile: string;
}

export interface RelojZkDto {
  id: string;
  nombre: string;
  ip: string;
  puerto: number;
  commKey: number;
  modo: 'directo' | 'mssql';
  activo: boolean;
  ultimaDescarga: string | null;
  ultimaCantidad: number;
}

export interface RelojZkInfoDto {
  nombre: string | null;
  serial: string | null;
  usuarios: number;
  marcas: number;
  ultimaMarca: string | null;
}

export interface RelojZkDescargaDto {
  id: string;
  relojZkId: string;
  relojNombre: string;
  fecha: string;
  leidas: number;
  nuevas: number;
  duplicadas: number;
  legajosDesconocidos: number;
  estado: string;
  mensaje: string | null;
}

export interface MarcaLeidaRelojZkDto {
  legajo: string;
  fechaHora: string;
  esSalida: boolean;
  enRango: boolean;
  legajoDesconocido: boolean;
  nueva: boolean;
}

export interface ResultadoDescargaRelojZkDto {
  leidas: number;
  nuevas: number;
  duplicadas: number;
  desconocidos: number;
  mensaje: string | null;
  marcas: MarcaLeidaRelojZkDto[];
}

// ---------- Reportes builder ----------

export type TipoDatoReporte = 'Texto' | 'Numero' | 'Fecha' | 'Booleano';
export type AgregacionReporte = 'Suma' | 'Promedio' | 'Maximo' | 'Minimo' | 'Conteo';
export type OperadorFiltro =
  | 'Igual' | 'Distinto' | 'Contiene' | 'NoContiene' | 'En'
  | 'Mayor' | 'MayorIgual' | 'Menor' | 'MenorIgual' | 'Entre' | 'Vacio' | 'NoVacio';
export type TipoGrafico = 'Barras' | 'Lineas' | 'Torta';

export interface CampoFilasDef { campo: string; alias: string; }
export interface CampoValorDef { campo: string; alias: string; agregacion: AgregacionReporte; }
export interface CampoColumnaDef { campo: string; alias: string; }
export interface CampoRelacionDef { campoPadre: string; campoHijo: string; }
export interface GraficoDef { titulo: string; tipo: TipoGrafico; campoX: string; camposY: string[]; }

export interface FiltroDef {
  campo: string;
  tipoDato: TipoDatoReporte;
  operador: OperadorFiltro;
  etiqueta: string | null;
  parametrizable: boolean;
  valor: string | null;
  valor2: string | null;
  valores: string[] | null;
}

export interface SubreporteDef { reporteId: string; nombre: string | null; camposRelacion: CampoRelacionDef[]; }

export interface ReporteDefinicionDto {
  filas: CampoFilasDef[];
  valores: CampoValorDef[];
  columnas: CampoColumnaDef[];
  filtros: FiltroDef[];
  graficos: GraficoDef[];
  subreportes: SubreporteDef[];
  orden: string | null;
  limite: number | null;
}

export interface ColumnaMetadata { nombre: string; tipo: TipoDatoReporte; }

export interface ReporteResumenDto {
  id: string;
  nombre: string;
  descripcion: string | null;
  activo: boolean;
  creadoPorEmail: string;
  puedeEditar: boolean;
  creadoEn: string;
}

export interface PermisoDto { email: string | null; rol: string | null; }

export interface ReporteCompletoDto {
  id: string;
  nombre: string;
  descripcion: string | null;
  querySql: string;
  activo: boolean;
  creadoPorEmail: string;
  puedeEditar: boolean;
  conexion: string;
  disenoJson: string;
  definicion: ReporteDefinicionDto;
  permisos: PermisoDto[];
}

export interface ResultadoReporteDto {
  columnas: string[];
  filas: Record<string, unknown>[];
  totales: Record<string, unknown> | null;
}

// ---------- Diseño de apariencia (canvas) ----------

export type TipoElementoDiseno = 'titulo' | 'texto' | 'imagen' | 'barcode' | 'linea' | 'tabla';

export interface ElementoDiseno {
  id: string;
  tipo: TipoElementoDiseno;
  x: number;
  y: number;
  ancho: number;
  alto: number;
  texto?: string;
  tamano?: number;
  negrita?: boolean;
  italica?: boolean;
  subrayado?: boolean;
  alineacion?: 'izq' | 'centro' | 'der';
  color?: string;
  imagen?: string;
  valor?: string;
  mostrarTexto?: boolean;
}

export interface ReporteDiseno {
  elementos: ElementoDiseno[];
}

export interface ColumnaTablaDto { nombre: string; tipo: string; }
export interface RelacionFkDto { tablaExterna: string; colLocal: string; colExterna: string; }
export interface TablaDetalleDto { esquema: string; tabla: string; columnas: ColumnaTablaDto[]; relaciones: RelacionFkDto[]; }
export interface TablaListaDto { esquema: string; tabla: string; }

// ---------- Dashboards ----------

export interface FiltroDashboardDef {
  campo: string;
  tipoDato: TipoDatoReporte;
  operador: OperadorFiltro;
  etiqueta: string | null;
  valor: string | null;
  valor2: string | null;
  valores: string[] | null;
}

export interface KpiDef {
  titulo: string;
  campo: string;
  agregacion: AgregacionReporte;
  formato: 'numero' | 'moneda' | 'porcentaje';
  esKpo: boolean;
  objetivo: number | null;
  color: string | null;
}

export interface GraficoDashboardDef {
  titulo: string;
  tipo: 'Barras' | 'Lineas' | 'Torta' | 'Area' | 'Dona';
  campoX: string;
  campoY: string;
  agregacion: AgregacionReporte;
  ordenarValorDesc: boolean;
  ancho: 'completo' | 'mitad';
}

export interface TablaDashboardDef { titulo: string; limite: number; }

export interface DashboardDefinicionDto {
  filtros: FiltroDashboardDef[];
  kpis: KpiDef[];
  graficos: GraficoDashboardDef[];
  tablas: TablaDashboardDef[];
  limite: number | null;
}

export interface DashboardCompletoDto {
  id: string;
  nombre: string;
  descripcion: string | null;
  querySql: string;
  activo: boolean;
  creadoPorEmail: string;
  puedeEditar: boolean;
  conexion: string;
  definicion: DashboardDefinicionDto;
}

export interface DashboardResumenDto {
  id: string;
  nombre: string;
  descripcion: string | null;
  activo: boolean;
  creadoPorEmail: string;
  puedeEditar: boolean;
}

export interface KpiResultadoDto {
  titulo: string;
  valor: number;
  formato: string;
  esKpo: boolean;
  objetivo: number | null;
  color: string | null;
}

export interface GraficoResultadoDto {
  titulo: string;
  tipo: string;
  anchoCompleto: boolean;
  datos: ResultadoReporteDto;
}

export interface TablaDashboardResultadoDto {
  titulo: string;
  datos: ResultadoReporteDto;
}

export interface ResultadoDashboardDto {
  kpis: KpiResultadoDto[];
  graficos: GraficoResultadoDto[];
  tablas: TablaDashboardResultadoDto[];
}
