using FluentValidation;
using MediatR;
using PortalIUPA.Application.DTOs;
using PortalIUPA.Application.Services;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Application.UseCases.Estadisticas;

public sealed record GetEstadisticasAccesosQuery(DateOnly Desde, DateOnly Hasta)
    : IRequest<EstadisticasAccesosDto>;

public sealed class GetEstadisticasAccesosQueryValidator : AbstractValidator<GetEstadisticasAccesosQuery>
{
    public GetEstadisticasAccesosQueryValidator()
    {
        RuleFor(q => q.Hasta).GreaterThanOrEqualTo(q => q.Desde)
            .WithMessage("La fecha de fin no puede ser anterior a la de inicio.");
    }
}

public sealed class GetEstadisticasAccesosQueryHandler : IRequestHandler<GetEstadisticasAccesosQuery,
    EstadisticasAccesosDto>
{
    private readonly IAccesoLogRepository _accesos;

    public GetEstadisticasAccesosQueryHandler(IAccesoLogRepository accesos) => _accesos = accesos;

    public async Task<EstadisticasAccesosDto> Handle(GetEstadisticasAccesosQuery request, CancellationToken ct)
    {
        var desde = request.Desde.ToDateTime(TimeOnly.MinValue);
        var hasta = request.Hasta.ToDateTime(TimeOnly.MaxValue);
        var logs = await _accesos.GetEntreAsync(desde, hasta, ct);

        return new EstadisticasAccesosDto(request.Desde, request.Hasta,
            AgregadorEstadisticas.PorSemana(logs),
            AgregadorEstadisticas.PorAccion(logs));
    }
}

public sealed record GetAccesosQuery(DateOnly Desde, DateOnly Hasta) : IRequest<IReadOnlyList<AccesoLogDto>>;

public sealed class GetAccesosQueryHandler : IRequestHandler<GetAccesosQuery, IReadOnlyList<AccesoLogDto>>
{
    private readonly IAccesoLogRepository _accesos;

    public GetAccesosQueryHandler(IAccesoLogRepository accesos) => _accesos = accesos;

    public async Task<IReadOnlyList<AccesoLogDto>> Handle(GetAccesosQuery request, CancellationToken ct)
    {
        var desde = request.Desde.ToDateTime(TimeOnly.MinValue);
        var hasta = request.Hasta.ToDateTime(TimeOnly.MaxValue);
        var logs = await _accesos.GetEntreAsync(desde, hasta, ct);

        return logs
            .OrderByDescending(l => l.FechaHora)
            .Select(l => new AccesoLogDto(l.Id, l.EmpleadoId, l.Correo, l.Accion, l.Ip, l.Dispositivo, l.FechaHora))
            .ToList();
    }
}