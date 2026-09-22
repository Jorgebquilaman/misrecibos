using Microsoft.Extensions.Options;
using PortalIUPA.Worker.Reloj.Data;
using PortalIUPA.Worker.Reloj.Device;
using PortalIUPA.Worker.Reloj.Models;

namespace PortalIUPA.Worker.Reloj;

/// <summary>Configuración completa del worker (appsettings.json, sin recompilar).</summary>
public sealed class RelojWorkerOptions
{
    public string Ip { get; set; } = "192.168.1.204";
    public int Puerto { get; set; } = 4370;
    public int CommKey { get; set; } = 0;
    public string NombreDispositivo { get; set; } = "SALA-3";
    public string ConnectionStringSql { get; set; } = string.Empty;
    /// <summary>Intervalo de polling en segundos (default 300 = 5 minutos).</summary>
    public int IntervaloSegundos { get; set; } = 300;
    /// <summary>Backoff exponencial ante fallas: duplica el delay hasta este máximo (segundos).</summary>
    public int BackoffMaximoSegundos { get; set; } = 1800;
    /// <summary>Borrar el log del dispositivo tras leerlo (OPCIONAL, default NUNCA).</summary>
    public bool BorrarLogTrasLeer { get; set; } = false;
    /// <summary>Deshabilitar el teclado/huellas del equipo durante la lectura (recomendado).</summary>
    public bool DeshabilitarDuranteLectura { get; set; } = true;
    public EsquemaProveedorOptions Esquema { get; set; } = new();
}

/// <summary>
/// BackgroundService que descarga las marcaciones del reloj biométrico ZKTeco 628C y las
/// inserta en la base SQL Server del software oficial del proveedor.
/// Ciclo: conectar (sesión corta) → deshabilitar → leer log → habilitar → filtrar lo nuevo
/// desde el checkpoint → upsert → actualizar checkpoint → salir.
/// Ante errores: backoff exponencial (duplica el delay hasta el máximo).
/// </summary>
public sealed class Worker : BackgroundService
{
    private readonly RelojWorkerOptions _opciones;
    private readonly ILogger<Worker> _logger;

    private TimeSpan _delayActual;

    public Worker(IOptions<RelojWorkerOptions> opciones, ILogger<Worker> logger)
    {
        _opciones = opciones.Value;
        _logger = logger;
        _delayActual = TimeSpan.FromSeconds(Math.Max(10, _opciones.IntervaloSegundos));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Worker de descarga de reloj iniciado: {Ip}:{Puerto} commKey={CommKey} → SQL Server.",
            _opciones.Ip, _opciones.Puerto, _opciones.CommKey);

        if (string.IsNullOrWhiteSpace(_opciones.ConnectionStringSql))
        {
            _logger.LogError("Falta ConnectionStringSql en la configuración. El worker no arranca a descargar.");
            return;
        }

        var repositorio = new AttendanceRepository(
            _opciones.ConnectionStringSql, _opciones.Esquema,
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<AttendanceRepository>());

        try
        {
            await repositorio.AsegurarCheckpointAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo asegurar la tabla de checkpoint. Revisar la conexión SQL.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EjecutarCicloAsync(repositorio, stoppingToken);
                _delayActual = TimeSpan.FromSeconds(_opciones.IntervaloSegundos);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // Backoff exponencial: duplica el delay hasta el máximo configurado.
                _delayActual = TimeSpan.FromSeconds(Math.Min(
                    _opciones.BackoffMaximoSegundos, _delayActual.TotalSeconds * 2));
                _logger.LogError(ex,
                    "Ciclo de descarga fallido. Próximo intento en {Segundos:N0}s (backoff).",
                    _delayActual.TotalSeconds);
            }

            await Task.Delay(_delayActual, stoppingToken);
        }
    }

    /// <summary>Un ciclo completo de descarga (expuesto para testearlo con un cliente fake).</summary>
    public async Task<int> EjecutarCicloAsync(AttendanceRepository repositorio, CancellationToken ct)
    {
        var checkpoint = await repositorio.ObtenerCheckpointAsync(_opciones.NombreDispositivo);

        // Sesión corta: conectar, leer, salir (el reloj acepta una sola sesión TCP a la vez).
        using var cliente = await ZkTcpClient.ConectarAsync(
            _opciones.Ip, _opciones.Puerto, _opciones.CommKey, null, ct);

        if (_opciones.DeshabilitarDuranteLectura)
            await cliente.DeshabilitarAsync(ct);

        List<AttendanceRecord> todas;
        try
        {
            todas = (await cliente.LeerMarcasAsync(ct)).ToList();
        }
        finally
        {
            if (_opciones.DeshabilitarDuranteLectura)
                await cliente.HabilitarAsync(ct);
        }

        // Solo registros NUEVOS desde el último checkpoint (y fechas válidas).
        var nuevas = todas
            .Where(r => r.Timestamp > checkpoint && r.Timestamp != DateTime.MinValue)
            .OrderBy(r => r.Timestamp)
            .ToList();

        if (nuevas.Count == 0)
        {
            _logger.LogDebug("Sin marcas nuevas desde {Checkpoint}.", checkpoint);
            return 0;
        }

        var insertadas = await repositorio.UpsertAsync(nuevas, _opciones.NombreDispositivo, ct);
        var nuevoCheckpoint = nuevas.Max(r => r.Timestamp);
        await repositorio.ActualizarCheckpointAsync(_opciones.NombreDispositivo, nuevoCheckpoint, ct);

        _logger.LogInformation(
            "Ciclo OK: {Leidas} leídas, {Nuevas} nuevas desde {Checkpoint}, {Insertadas} insertadas en SQL, checkpoint → {Nuevo}.",
            todas.Count, nuevas.Count, checkpoint, insertadas, nuevoCheckpoint);

        if (_opciones.BorrarLogTrasLeer)
        {
            _logger.LogWarning("Borrando el log de asistencia del dispositivo (BorrarLogTrasLeer=true).");
            await cliente.VaciarMarcasAsync(ct);
        }

        return insertadas;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker de reloj deteniéndose.");
        await base.StopAsync(cancellationToken);
    }
}
