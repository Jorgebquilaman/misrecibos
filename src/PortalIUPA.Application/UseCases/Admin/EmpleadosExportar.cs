using MediatR;
using PortalIUPA.Application.Common;
using PortalIUPA.Application.DTOs;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Application.UseCases.Admin;

/// <summary>Devuelve todos los empleados filtrados y ordenados (sin paginar) para exportar.</summary>
public sealed record ExportarEmpleadosQuery(string? Texto = null, string? Orden = "Apellido",
    bool Descendente = false) : IRequest<IReadOnlyList<EmpleadoDto>>;

public sealed class ExportarEmpleadosQueryHandler : IRequestHandler<ExportarEmpleadosQuery,
    IReadOnlyList<EmpleadoDto>>
{
    private readonly IEmpleadoRepository _empleados;
    private readonly IAreaRepository _areas;

    public ExportarEmpleadosQueryHandler(IEmpleadoRepository empleados, IAreaRepository areas)
    {
        _empleados = empleados;
        _areas = areas;
    }

    public async Task<IReadOnlyList<EmpleadoDto>> Handle(ExportarEmpleadosQuery request, CancellationToken ct)
    {
        var empleados = await _empleados.GetAllAsync(ct);
        var areas = (await _areas.GetAllAsync(ct)).ToDictionary(a => a.Id);
        return EmpleadosFiltro.FiltrarYOrdenar(empleados, areas, request.Texto, request.Orden,
            request.Descendente);
    }
}