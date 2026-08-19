using MediatR;
using PortalIUPA.Application.DTOs;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Application.UseCases.Licencias;

public sealed record GetTiposLicenciaQuery : IRequest<IReadOnlyList<TipoLicenciaDto>>;

public sealed class GetTiposLicenciaQueryHandler : IRequestHandler<GetTiposLicenciaQuery, IReadOnlyList<TipoLicenciaDto>>
{
    private readonly ITipoLicenciaRepository _tipos;

    public GetTiposLicenciaQueryHandler(ITipoLicenciaRepository tipos) => _tipos = tipos;

    public async Task<IReadOnlyList<TipoLicenciaDto>> Handle(GetTiposLicenciaQuery request, CancellationToken ct)
    {
        var tipos = await _tipos.GetActivosAsync(ct);
        return tipos
            .OrderBy(t => t.Nombre)
            .Select(t => new TipoLicenciaDto(t.Id, t.Nombre, t.LimiteMensual, t.LimiteAnual, t.Descripcion,
                t.RequiereAdjunto, t.Activo,
                t.NivelesOrdenados.Select(n => new NivelAprobacionDto(n.Id, n.Orden, n.RolRequerido)).ToList()))
            .ToList();
    }
}

/// <summary>Consumo de días por tipo de licencia (para mostrar al empleado cuánto le queda antes de solicitar).</summary>
public sealed record GetConsumoLicenciasQuery(Guid EmpleadoId, int Anio, int Mes)
    : IRequest<IReadOnlyList<ConsumoTipoLicenciaDto>>;

public sealed class GetConsumoLicenciasQueryHandler : IRequestHandler<GetConsumoLicenciasQuery,
    IReadOnlyList<ConsumoTipoLicenciaDto>>
{
    private readonly ITipoLicenciaRepository _tipos;
    private readonly ISolicitudLicenciaRepository _solicitudes;

    public GetConsumoLicenciasQueryHandler(ITipoLicenciaRepository tipos, ISolicitudLicenciaRepository solicitudes)
    {
        _tipos = tipos;
        _solicitudes = solicitudes;
    }

    public async Task<IReadOnlyList<ConsumoTipoLicenciaDto>> Handle(GetConsumoLicenciasQuery request,
        CancellationToken ct)
    {
        var tipos = await _tipos.GetActivosAsync(ct);
        var resultado = new List<ConsumoTipoLicenciaDto>();

        foreach (var tipo in tipos)
        {
            var (diasMes, diasAnio) = await _solicitudes.GetConsumoAsync(request.EmpleadoId, tipo.Id, request.Anio,
                request.Mes, ct);
            resultado.Add(new ConsumoTipoLicenciaDto(tipo.Id, tipo.Nombre, diasMes, diasAnio, tipo.LimiteMensual,
                tipo.LimiteAnual));
        }

        return resultado;
    }
}