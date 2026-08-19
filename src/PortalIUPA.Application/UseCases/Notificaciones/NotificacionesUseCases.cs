using MediatR;
using PortalIUPA.Application.DTOs;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Application.UseCases.Notificaciones;

public sealed record GetMisNotificacionesQuery(Guid EmpleadoId) : IRequest<IReadOnlyList<NotificacionDto>>;

public sealed class GetMisNotificacionesQueryHandler : IRequestHandler<GetMisNotificacionesQuery,
    IReadOnlyList<NotificacionDto>>
{
    private readonly INotificacionRepository _notificaciones;

    public GetMisNotificacionesQueryHandler(INotificacionRepository notificaciones) => _notificaciones = notificaciones;

    public async Task<IReadOnlyList<NotificacionDto>> Handle(GetMisNotificacionesQuery request, CancellationToken ct)
    {
        var notificaciones = await _notificaciones.GetByEmpleadoAsync(request.EmpleadoId, ct);
        return notificaciones
            .OrderByDescending(n => n.FechaCreacion)
            .Select(n => new NotificacionDto(n.Id, n.Tipo, n.Titulo, n.Cuerpo, n.Link, n.Leida, n.FechaCreacion))
            .ToList();
    }
}

public sealed record MarcarNotificacionLeidaCommand(Guid NotificacionId, Guid EmpleadoId) : IRequest<Unit>;

public sealed class MarcarNotificacionLeidaCommandHandler : IRequestHandler<MarcarNotificacionLeidaCommand, Unit>
{
    private readonly INotificacionRepository _notificaciones;

    public MarcarNotificacionLeidaCommandHandler(INotificacionRepository notificaciones) => _notificaciones = notificaciones;

    public async Task<Unit> Handle(MarcarNotificacionLeidaCommand request, CancellationToken ct)
    {
        var notificacion = await _notificaciones.GetByIdAsync(request.NotificacionId, ct)
            ?? throw new PortalIUPA.Application.Common.EntidadNoEncontradaException("La notificación no existe.");

        if (notificacion.EmpleadoId == request.EmpleadoId)
        {
            notificacion.MarcarLeida();
            await _notificaciones.UpdateAsync(notificacion, ct);
        }

        return Unit.Value;
    }
}

public sealed record GetNotificacionesNoLeidasCountQuery(Guid EmpleadoId) : IRequest<int>;

public sealed class GetNotificacionesNoLeidasCountQueryHandler : IRequestHandler<GetNotificacionesNoLeidasCountQuery, int>
{
    private readonly INotificacionRepository _notificaciones;

    public GetNotificacionesNoLeidasCountQueryHandler(INotificacionRepository notificaciones) =>
        _notificaciones = notificaciones;

    public async Task<int> Handle(GetNotificacionesNoLeidasCountQuery request, CancellationToken ct) =>
        await _notificaciones.GetNoLeidasCountAsync(request.EmpleadoId, ct);
}

public sealed record CrearNotificacionCommand(Guid EmpleadoId, string Titulo, string Cuerpo, string? Link = null)
    : IRequest<Unit>;

public sealed class CrearNotificacionCommandHandler : IRequestHandler<CrearNotificacionCommand, Unit>
{
    private readonly INotificacionRepository _notificaciones;

    public CrearNotificacionCommandHandler(INotificacionRepository notificaciones) => _notificaciones = notificaciones;

    public async Task<Unit> Handle(CrearNotificacionCommand request, CancellationToken ct)
    {
        await _notificaciones.AddAsync(new Notificacion(request.EmpleadoId,
            Domain.Enums.TipoNotificacion.Sistema, request.Titulo, request.Cuerpo, request.Link), ct);
        return Unit.Value;
    }
}