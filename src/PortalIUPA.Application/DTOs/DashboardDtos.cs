namespace PortalIUPA.Application.DTOs;

public sealed record AccesosPorSemanaDto(string Semana, DateOnly Inicio, DateOnly Fin, int Cantidad);

public sealed record AccesosPorAccionDto(string Accion, int Cantidad);

public sealed record AccesoLogDto(
    Guid Id,
    Guid? EmpleadoId,
    string Correo,
    string Accion,
    string? Ip,
    string? Dispositivo,
    DateTime FechaHora);

public sealed record EstadisticasAccesosDto(
    DateOnly Desde,
    DateOnly Hasta,
    IReadOnlyList<AccesosPorSemanaDto> PorSemana,
    IReadOnlyList<AccesosPorAccionDto> PorAccion);

public sealed record ResumenFichadasDto(
    int EmpleadosConMarcas,
    int DiasLaborables,
    double PromedioAsistenciaPct,
    double TotalHoras,
    int Tardanzas,
    string? DiaMasConcurrido,
    string? DiaMenosConcurrido,
    string? FranjaPico,
    string? HoraPromedioIngreso = null,
    string? HoraPromedioEgreso = null);

public sealed record AsistenciaDiaDto(DateOnly Fecha, string Dia, int Presentes, int Ausentes);

public sealed record DiaSemanaFichadasDto(string Dia, int TotalPresentes, double Promedio);

public sealed record FranjaFichadasDto(string Franja, int Entradas, int Salidas);

public sealed record TopFichadasEmpleadoDto(int Legajo, string Nombre, int Dias, double Horas, double PromedioHoras);

public sealed record TardanzasEmpleadoDto(int Legajo, string Nombre, int Tardanzas);

public sealed record AreaFichadasDto(string Area, int Empleados, double AsistenciaPct);

public sealed record EstadisticasFichadasDto(
    DateOnly Desde,
    DateOnly Hasta,
    ResumenFichadasDto Resumen,
    IReadOnlyList<AsistenciaDiaDto> PorDia,
    IReadOnlyList<DiaSemanaFichadasDto> PorDiaSemana,
    IReadOnlyList<FranjaFichadasDto> PorFranja,
    IReadOnlyList<TopFichadasEmpleadoDto> TopEmpleados,
    IReadOnlyList<TardanzasEmpleadoDto> TopTardanzas,
    IReadOnlyList<AreaFichadasDto> PorArea);

public sealed record LicenciasPorTipoDto(string TipoLicencia, int Cantidad, int Dias);

public sealed record DashboardRecibosDto(int TotalActivos, int Descargados, int Pendientes, IReadOnlyList<ReciboDisponibleDto> Disponibles);
public sealed record DashboardLicenciasDto(int EnEspera, int Aprobadas, int Desaprobadas, int Canceladas, IReadOnlyList<ConsumoTipoLicenciaDto> Consumo);
public sealed record DashboardFichadasDetalladoDto(int DiasTrabajados, int DiasConAnomalia, int Faltas, int Tardanzas, TimeSpan TotalHoras, TimeSpan? PromedioHoras, IReadOnlyList<JornadaDto> Jornadas);
public sealed record DashboardCvDto(int Experiencias, int Antecedentes, int Certificados, int CertificadosVerificados, double CompletitudPct, IReadOnlyList<CertificadoCvResumenDto> CertificadosPorTipo);
public sealed record CertificadoCvResumenDto(string Tipo, int Cantidad);
public sealed record DashboardCertificadosDto(int Total, int Generados);

public sealed record DashboardEmpleadoDto(
    ReciboDisponibleDto? UltimoRecibo,
    int LicenciasPendientes,
    int LicenciasAprobadas,
    int LicenciasRechazadas,
    ResumenJornadasDto? FichadasDelMes,
    IReadOnlyList<AnuncioDto> Anuncios,
    int NotificacionesNoLeidas,
    DashboardRecibosDto? Recibos = null,
    DashboardLicenciasDto? LicenciasDetallado = null,
    DashboardFichadasDetalladoDto? FichadasDetallado = null,
    DashboardCvDto? Cv = null,
    DashboardCertificadosDto? CertificadosResumen = null);

public sealed record DashboardEmpleadorDto(
    int PendientesDeMiAprobacion,
    int EmpleadosACargo,
    int TotalPendientes,
    IReadOnlyList<LicenciasPorTipoDto> LicenciasPorTipoDelMes,
    TimeSpan? TiempoPromedioAprobacion,
    IReadOnlyList<AccesosPorSemanaDto> AccesosUltimos7Dias,
    IReadOnlyList<AsistenciaPorEmpleadoDto> AsistenciaACargoDelMes);