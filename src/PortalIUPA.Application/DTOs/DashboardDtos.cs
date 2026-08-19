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

public sealed record LicenciasPorTipoDto(string TipoLicencia, int Cantidad, int Dias);

public sealed record DashboardEmpleadoDto(
    ReciboDisponibleDto? UltimoRecibo,
    int LicenciasPendientes,
    int LicenciasAprobadas,
    int LicenciasRechazadas,
    ResumenJornadasDto? FichadasDelMes,
    IReadOnlyList<AnuncioDto> Anuncios,
    int NotificacionesNoLeidas);

public sealed record DashboardEmpleadorDto(
    int PendientesDeMiAprobacion,
    int EmpleadosACargo,
    int TotalPendientes,
    IReadOnlyList<LicenciasPorTipoDto> LicenciasPorTipoDelMes,
    TimeSpan? TiempoPromedioAprobacion,
    IReadOnlyList<AccesosPorSemanaDto> AccesosUltimos7Dias,
    IReadOnlyList<AsistenciaPorEmpleadoDto> AsistenciaACargoDelMes);