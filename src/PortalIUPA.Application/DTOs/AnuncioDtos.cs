using PortalIUPA.Domain.Enums;

namespace PortalIUPA.Application.DTOs;

public sealed record AnuncioDto(
    Guid Id,
    string Titulo,
    string Cuerpo,
    DateOnly? FechaDesde,
    DateOnly? FechaHasta,
    PrioridadAnuncio Prioridad,
    TipoAnuncio Tipo,
    AlcanceAnuncio Alcance,
    Guid? AreaId,
    Rol? Rol,
    bool Activo,
    bool Leido,
    DateTime FechaCreacion);

public sealed record NotificacionDto(
    Guid Id,
    TipoNotificacion Tipo,
    string Titulo,
    string Cuerpo,
    string? Link,
    bool Leida,
    DateTime FechaCreacion);