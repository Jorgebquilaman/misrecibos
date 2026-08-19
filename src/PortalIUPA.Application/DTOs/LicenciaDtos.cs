using PortalIUPA.Domain.Enums;

namespace PortalIUPA.Application.DTOs;

public sealed record NivelAprobacionDto(Guid Id, int Orden, AprobadorRequerido RolRequerido);

public sealed record TipoLicenciaDto(
    Guid Id,
    string Nombre,
    int? LimiteMensual,
    int? LimiteAnual,
    string? Descripcion,
    bool RequiereAdjunto,
    bool Activo,
    IReadOnlyList<NivelAprobacionDto> Niveles);

public sealed record AprobacionDto(
    Guid Id,
    Guid NivelAprobacionId,
    int NivelOrden,
    string NivelRol,
    Guid AprobadorId,
    string AprobadorNombre,
    ResultadoAprobacion Resultado,
    string? Comentario,
    DateTime FechaHora);

public sealed record SolicitudLicenciaDto(
    Guid Id,
    Guid EmpleadoId,
    string EmpleadoNombre,
    Guid TipoLicenciaId,
    string TipoLicenciaNombre,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    int Dias,
    string? Asunto,
    string? Motivo,
    Guid? AdjuntoId,
    EstadoSolicitud Estado,
    DateTime FechaSolicitud,
    IReadOnlyList<AprobacionDto> Aprobaciones);

public sealed record SolicitudDetalleDto(
    SolicitudLicenciaDto Solicitud,
    IReadOnlyList<NivelAprobacionDto> Niveles,
    NivelAprobacionDto? NivelPendiente,
    bool PuedeAprobar,
    string? RazonBloqueo);

public sealed record ConsumoTipoLicenciaDto(
    Guid TipoLicenciaId,
    string TipoLicenciaNombre,
    int ConsumidosMes,
    int ConsumidosAnio,
    int? LimiteMensual,
    int? LimiteAnual);

public sealed record NuevaSolicitudResultado(Guid SolicitudId, EstadoSolicitud Estado);