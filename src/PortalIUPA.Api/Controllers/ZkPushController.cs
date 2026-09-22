using System.Text;
using Microsoft.AspNetCore.Mvc;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Api.Controllers;

/// <summary>
/// Receptor del protocolo ZK Push (ADMS): los relojes ZKTeco se conectan SOLOS a este endpoint
/// y envían cada marca en tiempo real, sin que nadie los consulte (así el firmware no se traba).
/// Configuración del equipo: menú COMM → Cloud Server / ADMS → IP del servidor + puerto.
/// Sin autenticación JWT: el equipo se identifica por su número de serie (SN) en la query.
/// </summary>
[ApiController]
[Route("iclock")]
public sealed class ZkPushController : ControllerBase
{
    private readonly IEmpleadoRepository _empleados;
    private readonly IMarcaRelojRepository _marcas;
    private readonly ILogger<ZkPushController> _logger;
    private static readonly SemaphoreSlim Insercion = new(1, 1);

    public ZkPushController(IEmpleadoRepository empleados, IMarcaRelojRepository marcas,
        ILogger<ZkPushController> logger)
    {
        _empleados = empleados;
        _marcas = marcas;
        _logger = logger;
    }

    /// <summary>Handshakes y heartbeats del equipo (GET sin body). Respondemos OK plano.</summary>
    [HttpGet("cdata")]
    public async Task<IActionResult> Get([FromQuery] string? SN, [FromQuery] string? table,
        [FromQuery] string? options, CancellationToken ct)
    {
        _logger.LogInformation("ZK Push GET: SN={SN} table={Table} options={Options} qs={Query}",
            SN, table, options, Request.QueryString.Value);

        if (!string.IsNullOrWhiteSpace(table) &&
            table.StartsWith("ATTLOG", StringComparison.OrdinalIgnoreCase))
        {
            // Algunos firmwares envían los registros por GET (body vacío): los parámetros vienen en query.
            var cantidad = await ProcesarMarcasAsync(SN, Request.QueryString.Value ?? "", ct);
            return Content(cantidad > 0 ? "OK" : "OK", "text/plain");
        }

        // Handshake inicial (options=all) y latidos: respondemos configuración mínima en texto plano.
        if (!string.IsNullOrWhiteSpace(options))
            return Content("OK", "text/plain");

        return Content("OK", "text/plain");
    }

    /// <summary>Envío de registros del equipo (POST con body). table=ATTLOG trae las marcas.</summary>
    [HttpPost("cdata")]
    public async Task<IActionResult> Post([FromQuery] string? SN, [FromQuery] string? table,
        CancellationToken ct)
    {
        Request.EnableBuffering();
        string body;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
            body = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;

        _logger.LogInformation("ZK Push POST: SN={SN} table={Table} body={Body}", SN, table,
            body.Length > 400 ? body[..400] + "..." : body);

        if (string.IsNullOrWhiteSpace(table) ||
            !table.StartsWith("ATTLOG", StringComparison.OrdinalIgnoreCase))
            return Content("OK", "text/plain");

        var cantidad = await ProcesarMarcasAsync(SN, body, ct);
        return Content(cantidad > 0 ? "OK" : "OK", "text/plain");
    }

    /// <summary>
    /// Procesa líneas de marcas: "legajo\tfecha hora\tverify\tstate[...]" (separadas por \n o \r).
    /// Inserta en marcas_reloj con dedupe (índice único empleado+fecha) y origen reloj-zk-push.
    /// </summary>
    private async Task<int> ProcesarMarcasAsync(string? sn, string datos, CancellationToken ct)
    {
        var lineas = datos.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var insertadas = 0;

        var legajosEmpleados = (await _empleados.GetAllAsync(ct))
            .GroupBy(e => e.Legajo).ToDictionary(g => g.Key, g => g.First());

        await Insercion.WaitAsync(ct);
        try
        {
            foreach (var linea in lineas)
            {
                var partes = linea.Split('\t');
                if (partes.Length < 4) continue;

                var legajoTexto = partes[0].Trim();
                if (!int.TryParse(legajoTexto, out var legajo) ||
                    !legajosEmpleados.TryGetValue(legajo, out var empleado))
                {
                    _logger.LogWarning("ZK Push: marca con legajo desconocido '{Legajo}' (SN={SN}).", legajoTexto, sn);
                    continue;
                }

                if (!DateTime.TryParse(partes[1].Trim(), out var fechaHora) ||
                    fechaHora < DateTime.Now.AddYears(-2) || fechaHora > DateTime.Now.AddDays(2))
                {
                    _logger.LogWarning("ZK Push: marca con fecha inválida '{Fecha}' legajo {Legajo}.", partes[1], legajo);
                    continue;
                }

                var estado = partes.Length > 3 && int.TryParse(partes[3].Trim(), out var s) ? s : 0;
                var tipo = estado == 1 ? TipoMarca.Salida : TipoMarca.Entrada;

                if (await _marcas.ExisteAsync(empleado.Id, fechaHora, ct)) continue;

                var marca = new MarcaReloj(empleado.Id, fechaHora, tipo, "reloj-zk-push");
                await _marcas.AddRangeAsync(new[] { marca }, ct);
                insertadas++;
            }
        }
        finally
        {
            Insercion.Release();
        }

        if (insertadas > 0)
            _logger.LogInformation("ZK Push: {Insertadas} marcas nuevas insertadas (SN={SN}).", insertadas, sn);
        return insertadas;
    }
}
