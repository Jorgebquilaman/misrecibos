using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using PortalIUPA.Application.Common;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Infrastructure.External.ZK;

public sealed record MarcaLeidaRelojZk(string Legajo, DateTime FechaHora, bool EsSalida, bool EnRango,
    bool LegajoDesconocido, bool Nueva);

public sealed record ResultadoDescargaRelojZk(int Leidas, int Nuevas, int Duplicadas, int Desconocidos,
    string? Mensaje, IReadOnlyList<MarcaLeidaRelojZk> Marcas);

/// <summary>
/// Orquesta la descarga de marcas desde relojes ZKTeco. Dos modos:
/// - "directo": protocolo ZK por TCP al equipo (ZkDeviceClient).
/// - "mssql": lee las marcas que el software ZKBio ya descargó a la base del reloj (checkinout).
///   Solo lectura del MSSQL; las marcas importadas se guardan en PostgreSQL.
/// </summary>
public sealed class ServicioRelojesZk
{
    private const string OrigenMarcaZk = "reloj-zk";

    private readonly IEmpleadoRepository _empleados;
    private readonly IMarcaRelojRepository _marcas;
    private readonly IRelojZkRepository _relojes;
    private readonly IRelojZkDescargaRepository _descargas;
    private readonly IRelojDataSource _fuenteReloj;
    private readonly ILogger<ServicioRelojesZk> _logger;

    public ServicioRelojesZk(IEmpleadoRepository empleados, IMarcaRelojRepository marcas,
        IRelojZkRepository relojes, IRelojZkDescargaRepository descargas, IRelojDataSource fuenteReloj,
        ILogger<ServicioRelojesZk> logger)
    {
        _empleados = empleados;
        _marcas = marcas;
        _relojes = relojes;
        _descargas = descargas;
        _fuenteReloj = fuenteReloj;
        _logger = logger;
    }

    public async Task<RelojZkInfo> ProbarConexionAsync(RelojZk reloj, CancellationToken ct = default)
    {
        try
        {
            if (reloj.Modo == RelojZk.ModoMssql)
                return await ProbarMssqlAsync(ct);

            using var cliente = await ZkDeviceClient.ConectarAsync(reloj.Ip, reloj.Puerto, reloj.CommKey, ct);
            return await cliente.ObtenerInfoAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ReglaDeNegocioException(MensajeDeError(ex));
        }
    }

    private async Task<RelojZkInfo> ProbarMssqlAsync(CancellationToken ct)
    {
        var desde = DateTime.Today.AddDays(-30);
        var hasta = DateTime.Now.AddDays(1);
        var marcas = await _fuenteReloj.ObtenerMarcasAsync(null, desde, hasta, ct);
        var ultima = marcas.Count > 0 ? marcas.Max(m => m.FechaHora) : (DateTime?)null;
        return new RelojZkInfo("ZKBio (base del reloj)", "lectura por checkinout", 0, marcas.Count, ultima);
    }

    /// <param name="desde">Opcional: fecha mínima de las marcas a importar.</param>
    /// <param name="hasta">Opcional: fecha máxima de las marcas a importar.</param>
    /// <param name="registrarLogSiempre">False = solo registra el log cuando hay marcas nuevas (para la sincronización automática).</param>
    public async Task<ResultadoDescargaRelojZk> DescargarAsync(RelojZk reloj, DateTime? desde, DateTime? hasta,
        string? usuarioCorreo, CancellationToken ct = default, bool registrarLogSiempre = true)
    {
        var ahora = DateTime.Now;
        var rangoDesde = desde ?? (reloj.UltimaDescarga?.AddDays(-1) ?? ahora.AddDays(-90));
        var rangoHasta = hasta ?? ahora.AddDays(1);

        try
        {
            List<MarcaZkCruda> crudas;
            if (reloj.Modo == RelojZk.ModoMssql)
            {
                // Lectura del checkinout (MSSQL) que llena ZKBio. El dataSource ya trae el legajo y el tipo.
                var fuente = await _fuenteReloj.ObtenerMarcasAsync(null, rangoDesde, rangoHasta, ct);
                crudas = fuente
                    .Select(m => new MarcaZkCruda(m.Legajo.ToString(), m.FechaHora, 0,
                        m.Tipo == TipoMarca.Salida ? 1 : 0))
                    .ToList();
            }
            else
            {
                using var cliente = await ZkDeviceClient.ConectarAsync(reloj.Ip, reloj.Puerto, reloj.CommKey, ct);
                try
                {
                    crudas = (await cliente.LeerMarcasAsync(ct)).ToList();
                }
                finally
                {
                    await cliente.DesconectarAsync(ct);
                }
            }

            var enRango = crudas
                .Where(m => m.FechaHora >= rangoDesde && m.FechaHora <= rangoHasta && m.FechaHora != DateTime.MinValue)
                .ToList();

            var resultado = await ImportarAsync(reloj, crudas, enRango, rangoDesde, rangoHasta, ahora, usuarioCorreo, registrarLogSiempre, ct);
            return resultado;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Reloj ZK '{Nombre}': error al descargar marcas (modo {Modo}).", reloj.Nombre, reloj.Modo);
            var mensaje = MensajeDeError(ex);
            // En modo automático no registramos cada fallo para no llenar el log de descargas,
            // salvo que haya fallado una descarga manual previa.
            if (registrarLogSiempre)
                await _descargas.AddAsync(RelojZkDescarga.Fallida(reloj.Id, ahora, mensaje, usuarioCorreo), ct);
            throw new ReglaDeNegocioException($"No se pudo descargar las marcas: {mensaje}");
        }
    }

    /// <summary>Mapea legajo→empleado, deduplica y guarda. Compartido por ambos modos.
    /// Devuelve también el detalle marca por marca (para la ventana de descarga en la página).</summary>
    private async Task<ResultadoDescargaRelojZk> ImportarAsync(RelojZk reloj, List<MarcaZkCruda> crudas,
        List<MarcaZkCruda> enRango, DateTime rangoDesde, DateTime rangoHasta, DateTime ahora, string? usuarioCorreo,
        bool registrarLogSiempre, CancellationToken ct)
    {
        // Legajo → empleado
        var empleados = await _empleados.GetAllAsync(ct);
        var porLegajo = empleados.GroupBy(e => e.Legajo).ToDictionary(g => g.Key, g => g.First());

        // Deduplicar contra lo ya guardado (índice único empleado+fecha_hora) y dentro del lote
        var existentes = (await _marcas.GetByFechaBetweenAsync(rangoDesde, rangoHasta, ct))
            .Select(m => (m.EmpleadoId, m.FechaHora))
            .ToHashSet();
        var desconocidos = 0;
        var duplicadas = 0;
        var nuevas = new List<MarcaReloj>();
        var detalle = new List<MarcaLeidaRelojZk>();

        foreach (var cruda in crudas)
        {
            var enRangoFlag = cruda.FechaHora >= rangoDesde && cruda.FechaHora <= rangoHasta &&
                              cruda.FechaHora != DateTime.MinValue;
            Empleado? empleado = null;
            var conocida = int.TryParse(cruda.Legajo, out var legajo) && porLegajo.TryGetValue(legajo, out empleado);
            var nueva = false;

            if (enRangoFlag && conocida)
            {
                if (existentes.Add((empleado!.Id, cruda.FechaHora)))
                {
                    nueva = true;
                    var tipo = cruda.Estado == 1 ? TipoMarca.Salida : TipoMarca.Entrada;
                    nuevas.Add(new MarcaReloj(empleado.Id, cruda.FechaHora, tipo, OrigenMarcaZk));
                }
                else
                {
                    duplicadas++;
                }
            }
            else if (enRangoFlag)
            {
                desconocidos++;
            }

            detalle.Add(new MarcaLeidaRelojZk(cruda.Legajo, cruda.FechaHora, cruda.Estado == 1,
                enRangoFlag, !conocida, nueva));
        }
        detalle.Sort((a, b) => b.FechaHora.CompareTo(a.FechaHora));

        if (nuevas.Count > 0)
            await _marcas.AddRangeAsync(nuevas, ct);

        // Sincronización inversa: las marcas nuevas de la descarga se insertan también en el MSSQL
        // del checador (checkinout) si no existen, para que ZKBio tenga la foto completa.
        // Un fallo aquí no rompe la descarga: ya quedaron en PostgreSQL.
        var pendientesMssql = detalle
            .Where(d => d.Nueva && int.TryParse(d.Legajo, out _))
            .Select(d => (int.Parse(d.Legajo), d.FechaHora, d.EsSalida ? TipoMarca.Salida : TipoMarca.Entrada))
            .ToList();
        if (pendientesMssql.Count > 0)
        {
            try
            {
                var insertadas = await _fuenteReloj.SincronizarMarcasMssqlAsync(pendientesMssql, ct);
                if (insertadas > 0)
                    _logger.LogInformation("Reloj ZK '{Nombre}': {Insertadas} marcas sincronizadas al MSSQL.", reloj.Nombre, insertadas);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Reloj ZK '{Nombre}': no se pudieron sincronizar {Cantidad} marcas al MSSQL (queda en PostgreSQL).",
                    reloj.Nombre, pendientesMssql.Count);
            }
        }

        reloj.RegistrarDescarga(ahora, nuevas.Count);
        await _relojes.UpdateAsync(reloj, ct);
        // En sincronización automática solo registramos en el historial cuando aportó algo,
        // para que el log de descargas sea útil y no un ruido cada 15 minutos.
        if (registrarLogSiempre || nuevas.Count > 0)
            await _descargas.AddAsync(RelojZkDescarga.Exitosa(reloj.Id, ahora, enRango.Count, nuevas.Count,
                duplicadas, desconocidos, usuarioCorreo), ct);

        _logger.LogInformation(
            "Reloj ZK '{Nombre}' (modo {Modo}): {Leidas} marcas en rango, {Nuevas} nuevas, {Dup} duplicadas, {Desc} legajos desconocidos.",
            reloj.Nombre, reloj.Modo, enRango.Count, nuevas.Count, duplicadas, desconocidos);

        return new ResultadoDescargaRelojZk(enRango.Count, nuevas.Count, duplicadas, desconocidos, null, detalle);
    }

    public async Task VaciarAsync(RelojZk reloj, CancellationToken ct = default)
    {
        if (reloj.Modo == RelojZk.ModoMssql)
            throw new ReglaDeNegocioException(
                "El modo 'Vía ZKBio (MSSQL)' no permite vaciar la memoria del equipo: las marcas las gestiona el software ZKBio.");

        try
        {
            using var cliente = await ZkDeviceClient.ConectarAsync(reloj.Ip, reloj.Puerto, reloj.CommKey, ct);
            await cliente.VaciarMarcasAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ReglaDeNegocioException(MensajeDeError(ex));
        }
        _logger.LogInformation("Reloj ZK '{Nombre}': registros de asistencia vaciados.", reloj.Nombre);
    }

    private static string MensajeDeError(Exception ex) => ex switch
    {
        RelojNoDisponibleException => ex.Message,
        SocketException se => $"No se pudo conectar al reloj ({se.SocketErrorCode}).",
        IOException => "El reloj cerró la conexión o respondió con formato inválido.",
        InvalidOperationException => ex.Message,
        _ => $"Error inesperado: {ex.Message}"
    };
}
