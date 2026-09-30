using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZkAttendanceService.Data;
using ZkAttendanceService.Device;
using ZkAttendanceService.Options;

namespace ZkAttendanceService;

public sealed class Worker : BackgroundService
{
    private readonly ZkDeviceOptions _deviceOptions;
    private readonly AttendanceRepository _repository;
    private readonly ILogger<Worker> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly TimeSpan _pollInterval;

    public Worker(
        ZkDeviceOptions deviceOptions,
        AttendanceRepository repository,
        ILogger<Worker> logger,
        ILoggerFactory loggerFactory)
    {
        _deviceOptions = deviceOptions;
        _repository = repository;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _pollInterval = TimeSpan.FromMinutes(deviceOptions.PollIntervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el ciclo de descarga de marcaciones.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        // Sesión corta a propósito: el reloj típicamente solo acepta una conexión
        // TCP activa por vez, y el software oficial del proveedor puede querer
        // conectarse también. Conectar, leer, desconectar — no mantener la sesión
        // abierta entre ciclos.
        await using var client = new ZkTcpClient(
            _deviceOptions.Host,
            _deviceOptions.Port,
            _deviceOptions.CommKey,
            _deviceOptions.TimeoutMs,
            _loggerFactory.CreateLogger<ZkTcpClient>());

        await client.ConnectAsync(ct);

        try
        {
            await client.DisableDeviceAsync(ct);

            var raw = await client.DownloadAttendanceLogRawAsync(ct);

            if (raw.Length > 0)
            {
                var records = AttendanceLogParser.Parse(raw, _logger);
                await _repository.UpsertAsync(records, ct);
            }
        }
        finally
        {
            await client.EnableDeviceAsync(ct);
            await client.DisconnectAsync(ct);
        }
    }
}
