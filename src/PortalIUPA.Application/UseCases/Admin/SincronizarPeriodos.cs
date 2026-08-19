using MediatR;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Application.UseCases.Admin;

public sealed record SincronizarPeriodosResultDto(int Importados, int Actualizados, int SinCambios, int ConError, int TotalExternos);

public sealed record SincronizarPeriodosCommand : IRequest<SincronizarPeriodosResultDto>;

/// <summary>
/// Importa los períodos de liquidación desde SIU-Mapuche (tabla dh22) al portal.
/// - Si ya existe una liquidación con el mismo NroLiq (o código), se actualiza la descripción
///   conservando el estado activo elegido por el administrador.
/// - Si es nueva, se crea y se activa automáticamente cuando la liquidación está cerrada
///   (disponible para descargar el recibo).
/// </summary>
public sealed class SincronizarPeriodosCommandHandler : IRequestHandler<SincronizarPeriodosCommand, SincronizarPeriodosResultDto>
{
    private readonly IPeriodosDataSource _fuente;
    private readonly IPeriodoRepository _periodos;

    public SincronizarPeriodosCommandHandler(IPeriodosDataSource fuente, IPeriodoRepository periodos)
    {
        _fuente = fuente;
        _periodos = periodos;
    }

    public async Task<SincronizarPeriodosResultDto> Handle(SincronizarPeriodosCommand request, CancellationToken ct)
    {
        var externos = await _fuente.ObtenerPeriodosAsync(ct);
        var existentes = await _periodos.GetAllAsync(ct);

        var importados = 0;
        var actualizados = 0;
        var sinCambios = 0;
        var conError = 0;

        foreach (var ext in externos)
        {
            try
            {
                var existente = existentes.FirstOrDefault(p => p.NroLiq == ext.NroLiq)
                    ?? existentes.FirstOrDefault(p => p.Codigo == ext.Codigo);

                if (existente is not null)
                {
                    if (existente.Descripcion != ext.Descripcion || existente.NroLiq != ext.NroLiq)
                    {
                        existente.Actualizar(ext.Descripcion, ext.NroLiq);
                        await _periodos.UpdateAsync(existente, ct);
                        actualizados++;
                    }
                    else
                    {
                        sinCambios++;
                    }
                }
                else
                {
                    var nuevo = new Periodo(ext.Codigo, ext.Descripcion, ext.NroLiq);
                    if (ext.Disponible) nuevo.Activar();
                    await _periodos.AddAsync(nuevo, ct);
                    importados++;
                }
            }
            catch
            {
                conError++;
            }
        }

        return new SincronizarPeriodosResultDto(importados, actualizados, sinCambios, conError, externos.Count);
    }
}