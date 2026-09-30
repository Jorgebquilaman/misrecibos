using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Infrastructure.External.ZK;

/// <summary>
/// Descargador automático de marcas: cada 3 minutos intenta descargar SOLO los relojes que
/// tienen el casillero "Sincronización automática" tildado (en cualquiera de sus modos:
/// directo por protocolo ZK, o vía MSSQL de ZKBio). Con el casillero en falso el reloj se
/// descarga únicamente de forma manual.
/// Vive dentro del API, así sobrevive reinicios del servidor sin scripts en /tmp.
/// Solo registra en el historial cuando aporta marcas nuevas (para no ensuciar el log de descargas).
/// </summary>
public sealed class DescargaAutomaticaRelojesService : BackgroundService
{
    private static readonly TimeSpan Periodo = TimeSpan.FromMinutes(3);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<DescargaAutomaticaRelojesService> _logger;

    public DescargaAutomaticaRelojesService(IServiceScopeFactory scopes, ILogger<DescargaAutomaticaRelojesService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("Descarga automática de relojes ZK (modo directo) iniciada, periodo {Periodo}.", Periodo);
        // Pequeña espera inicial para que el API termine de arrancar.
        await Task.Delay(TimeSpan.FromSeconds(20), ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var relojes = scope.ServiceProvider.GetRequiredService<IRelojZkRepository>();
                var servicio = scope.ServiceProvider.GetRequiredService<ServicioRelojesZk>();

                var automaticos = (await relojes.GetAllAsync(ct))
                    .Where(r => r.Activo && r.SincronizacionAutomatica)
                    .ToList();

                foreach (var reloj in automaticos)
                {
                    try
                    {
                        var resultado = await servicio.DescargarAsync(
                            reloj, null, null, "auto", ct, registrarLogSiempre: false);
                        if (resultado.Nuevas > 0)
                            _logger.LogInformation(
                                "Descarga automática de '{Nombre}': {Nuevas} nuevas importadas.",
                                reloj.Nombre, resultado.Nuevas);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        // Falla esperada cuando el equipo está apagado o trabado: se reintenta al próximo ciclo.
                        _logger.LogDebug(ex, "Descarga automática de '{Nombre}' sin éxito (se reintenta).", reloj.Nombre);
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Descarga automática de relojes: error del ciclo.");
            }

            await Task.Delay(Periodo, ct);
        }
    }
}
