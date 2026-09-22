using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Infrastructure.External.ZK;

/// <summary>
/// Descargador automático de marcas: cada 3 minutos intenta descargar los relojes en modo
/// "directo" (protocolo ZK TCP) activos. Es el equivalente del rescatador externo pero vive
/// dentro del API, así sobrevive reinicios del servidor sin scripts en /tmp.
/// Solo registra en el historial cuando aporta marcas nuevas (para no ensuciar el log de descargas).
/// </summary>
public sealed class DescargaDirectoRelojesService : BackgroundService
{
    private static readonly TimeSpan Periodo = TimeSpan.FromMinutes(3);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<DescargaDirectoRelojesService> _logger;

    public DescargaDirectoRelojesService(IServiceScopeFactory scopes, ILogger<DescargaDirectoRelojesService> logger)
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

                var directos = (await relojes.GetAllAsync(ct))
                    .Where(r => r.Activo && r.Modo == RelojZk.ModoDirecto)
                    .ToList();

                foreach (var reloj in directos)
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
