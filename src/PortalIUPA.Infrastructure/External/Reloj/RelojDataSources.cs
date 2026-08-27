using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PortalIUPA.Application.Common;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Infrastructure.External.Reloj;

public sealed class RelojOptions
{
    /// <summary>Fuente de las marcas: "SqlServer" (base del checador) o "Mock" (datos simulados para dev).</summary>
    public string Fuente { get; set; } = "Mock";

    /// <summary>Conexión a la base del checador (SQL Server, DB "reloj").</summary>
    public string? SqlServerConnectionString { get; set; }

    /// <summary>Legajos simulados por la fuente Mock (separados por coma).</summary>
    public string MockLegajos { get; set; } = "1,2,3,4,5,6";
}

/// <summary>
/// Adaptador de solo lectura a la base SQL Server del reloj biométrico.
/// El legajo es la columna BADGENUMBER de userinfo (relacionada por USERID con las marcas
/// de checkinout); las marcas se leen de checkinout (CHECKTIME, CHECKTYPE: 0=entrada, 1=salida).
/// Consultas 100% parametrizadas; el job de sincronización copia a PostgreSQL.
/// </summary>
public sealed class SqlServerRelojDataSource : IRelojDataSource
{
    /// <summary>Circuit breaker: si el MSSQL no responde, se evita reintentar por este tiempo (la lectura
    /// cae al fallback de marcas sincronizadas en PostgreSQL).</summary>
    private static readonly TimeSpan VentanaReintento = TimeSpan.FromMinutes(5);
    private static DateTime _caidoHasta = DateTime.MinValue;
    private static readonly object Lock = new();

    private readonly RelojOptions _options;
    private readonly ILogger<SqlServerRelojDataSource> _logger;

    public SqlServerRelojDataSource(IOptions<RelojOptions> options, ILogger<SqlServerRelojDataSource> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MarcaRelojCruda>> ObtenerMarcasAsync(int? legajo, DateTime desde, DateTime hasta,
        CancellationToken ct = default)
    {
        if (DateTime.UtcNow < _caidoHasta)
        {
            var restante = _caidoHasta - DateTime.UtcNow;
            throw new RelojNoDisponibleException(
                $"El reloj no está disponible. Reintente en {(int)restante.TotalSeconds} segundos.", restante);
        }
        if (string.IsNullOrWhiteSpace(_options.SqlServerConnectionString))
            throw new RelojNoDisponibleException("Reloj no configurado (sin SqlServerConnectionString).");

        Exception? ultimoError = null;
        for (var intento = 1; intento <= 2; intento++)
        {
            try
            {
                var marcas = await LeerMarcasAsync(legajo, desde, hasta, ct);
                lock (Lock) { _caidoHasta = DateTime.MinValue; }
                return marcas;
            }
            catch (OperationCanceledException) { throw; }
            catch (RelojNoDisponibleException) { throw; }
            catch (Exception ex)
            {
                ultimoError = ex;
                _logger.LogWarning(ex, "Reloj: intento {Intento}/2 falló al leer marcas del checador (legajo {Legajo}).", intento, legajo);
                SqlConnection.ClearAllPools();
                if (intento < 2) await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }

        lock (Lock) { _caidoHasta = DateTime.UtcNow + VentanaReintento; }
        _logger.LogError(ultimoError, "Reloj: no se pudieron leer las marcas tras 2 intentos.");
        throw new RelojNoDisponibleException(
            "No se pudo consultar el reloj (MSSQL). Verifique la conexión o intente nuevamente en unos minutos.", VentanaReintento);
    }

    private async Task<IReadOnlyList<MarcaRelojCruda>> LeerMarcasAsync(int? legajo, DateTime desde, DateTime hasta, CancellationToken ct)
    {
        var marcas = new List<MarcaRelojCruda>();
        await using var conexion = new SqlConnection(_options.SqlServerConnectionString);
        await conexion.OpenAsync(ct);

        const string sql = """
            SELECT ui.BADGENUMBER, ci.CHECKTIME, ci.CHECKTYPE, ci.SENSORID
            FROM checkinout ci
            INNER JOIN userinfo ui ON ui.USERID = ci.USERID
            WHERE ci.CHECKTIME >= @Desde AND ci.CHECKTIME <= @Hasta
              AND (@Legajo IS NULL OR ui.BADGENUMBER = @Legajo)
            ORDER BY ci.CHECKTIME
            """;
        await using var comando = new SqlCommand(sql, conexion);
        comando.CommandTimeout = 30;
        comando.Parameters.Add("@Desde", SqlDbType.DateTime2).Value = desde;
        comando.Parameters.Add("@Hasta", SqlDbType.DateTime2).Value = hasta;
        comando.Parameters.Add("@Legajo", SqlDbType.Int).Value = legajo.HasValue ? (object)legajo.Value : DBNull.Value;

        await using var lector = await comando.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
        {
            if (!TryParseLegajo(lector["BADGENUMBER"], out var legajoLeido))
                continue;

            marcas.Add(new MarcaRelojCruda(
                legajoLeido,
                lector.GetDateTime(1),
                ParseTipo(lector["CHECKTYPE"]),
                lector["SENSORID"] is DBNull ? null : Convert.ToString(lector["SENSORID"])));
        }
        return marcas;
    }

    public async Task<bool> RegistrarMarcaAsync(int legajo, DateTime fechaHora, TipoMarca tipo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SqlServerConnectionString))
        {
            _logger.LogWarning("Reloj: no hay SqlServerConnectionString configurada. No se registró marca manual para legajo {Legajo}.", legajo);
            return false;
        }

        // Reintentos con conexión limpia: los timeouts de handshake pre-login contra el MSSQL del reloj
        // suelen ser transitorios (túnel saturado); reutilizar el pool puede reintentar sobre una conexión muerta.
        Exception? ultimoError = null;
        for (var intento = 1; intento <= 2; intento++)
        {
            try
            {
                return await InsertarMarcaAsync(legajo, fechaHora, tipo, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ultimoError = ex;
                _logger.LogWarning(ex, "Reloj: intento {Intento}/2 falló al registrar marca manual en MSSQL para legajo {Legajo}.",
                    intento, legajo);
                SqlConnection.ClearAllPools();
                if (intento < 2) await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }

        _logger.LogError(ultimoError, "Reloj: no se pudo registrar la marca manual en MSSQL para legajo {Legajo} tras 2 intentos.", legajo);
        return false;
    }

    private async Task<bool> InsertarMarcaAsync(int legajo, DateTime fechaHora, TipoMarca tipo, CancellationToken ct)
    {
        await using var conexion = new SqlConnection(_options.SqlServerConnectionString);
        await conexion.OpenAsync(ct);

        // Resolver USERID a partir del legajo (BADGENUMBER)
        int userId;
        await using (var cmdUser = new SqlCommand("SELECT USERID FROM userinfo WHERE BADGENUMBER = @Legajo", conexion))
        {
            cmdUser.Parameters.Add("@Legajo", SqlDbType.VarChar).Value = legajo.ToString();
            var result = await cmdUser.ExecuteScalarAsync(ct);
            if (result is null || result is DBNull)
            {
                _logger.LogWarning("Reloj: no se encontró USERID para legajo {Legajo} en userinfo. No se registró marca.", legajo);
                return false;
            }
            userId = Convert.ToInt32(result);
        }

        // Esquema checkinout: CHECKTYPE varchar(1) ('I'/'O'), SENSORID varchar(5), sn varchar(20)
        var checkType = tipo == TipoMarca.Entrada ? "I" : "O";
        const string sqlInsert = """
            INSERT INTO checkinout (USERID, CHECKTIME, CHECKTYPE, VERIFYCODE, SENSORID, Memoinfo, WorkCode, sn, UserExtFmt)
            VALUES (@UserId, @CheckTime, @CheckType, 1, 'WEB', 'Marca manual portal', 0, @Sn, 1)
            """;
        await using var cmdInsert = new SqlCommand(sqlInsert, conexion);
        cmdInsert.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
        cmdInsert.Parameters.Add("@CheckTime", SqlDbType.DateTime).Value = fechaHora;
        cmdInsert.Parameters.Add("@CheckType", SqlDbType.VarChar, 1).Value = checkType;
        cmdInsert.Parameters.Add("@Sn", SqlDbType.VarChar, 20).Value = "PORTAL-IUPA";

        var filas = await cmdInsert.ExecuteNonQueryAsync(ct);
        _logger.LogInformation("Reloj: marca manual registrada en MSSQL para legajo {Legajo} USERID {UserId} fecha {Fecha} tipo {Tipo}.", legajo, userId, fechaHora, tipo);
        return filas > 0;
    }

    public async Task<bool> EditarMarcaAsync(int legajo, DateTime fechaHoraVieja, TipoMarca tipoViejo,
        DateTime fechaHoraNueva, TipoMarca tipoNuevo, CancellationToken ct = default)
    {
        var resuelto = await ResolverUsuarioAsync(legajo, ct);
        if (resuelto is null) return false;

        await using var conexion = resuelto.Value.Conexion;
        try
        {
            const string sql = """
                UPDATE checkinout
                SET CHECKTIME = @NuevaFecha, CHECKTYPE = @NuevoTipo, Memoinfo = 'Marca editada portal'
                WHERE USERID = @UserId AND CHECKTIME = @ViejaFecha AND CHECKTYPE = @ViejoTipo
                  AND sn = @Sn
                """;
            await using var cmd = new SqlCommand(sql, conexion);
            cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = resuelto.Value.UserId;
            cmd.Parameters.Add("@ViejaFecha", SqlDbType.DateTime).Value = fechaHoraVieja;
            cmd.Parameters.Add("@ViejoTipo", SqlDbType.VarChar, 1).Value = tipoViejo == TipoMarca.Entrada ? "I" : "O";
            cmd.Parameters.Add("@NuevaFecha", SqlDbType.DateTime).Value = fechaHoraNueva;
            cmd.Parameters.Add("@NuevoTipo", SqlDbType.VarChar, 1).Value = tipoNuevo == TipoMarca.Entrada ? "I" : "O";
            cmd.Parameters.Add("@Sn", SqlDbType.VarChar, 20).Value = "PORTAL-IUPA";

            var filas = await cmd.ExecuteNonQueryAsync(ct);
            _logger.LogInformation("Reloj: marca manual editada en MSSQL para legajo {Legajo} ({Filas} fila(s)).", legajo, filas);
            return filas > 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Reloj: error al editar marca manual en MSSQL para legajo {Legajo}.", legajo);
            return false;
        }
    }

    public async Task<bool> EliminarMarcaAsync(int legajo, DateTime fechaHora, TipoMarca tipo, CancellationToken ct = default)
    {
        var resuelto = await ResolverUsuarioAsync(legajo, ct);
        if (resuelto is null) return false;

        await using var conexion = resuelto.Value.Conexion;
        try
        {
            const string sql = """
                DELETE FROM checkinout
                WHERE USERID = @UserId AND CHECKTIME = @CheckTime AND CHECKTYPE = @CheckType
                  AND sn = @Sn
                """;
            await using var cmd = new SqlCommand(sql, conexion);
            cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = resuelto.Value.UserId;
            cmd.Parameters.Add("@CheckTime", SqlDbType.DateTime).Value = fechaHora;
            cmd.Parameters.Add("@CheckType", SqlDbType.VarChar, 1).Value = tipo == TipoMarca.Entrada ? "I" : "O";
            cmd.Parameters.Add("@Sn", SqlDbType.VarChar, 20).Value = "PORTAL-IUPA";

            var filas = await cmd.ExecuteNonQueryAsync(ct);
            _logger.LogInformation("Reloj: marca manual eliminada en MSSQL para legajo {Legajo} ({Filas} fila(s)).", legajo, filas);
            return filas > 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Reloj: error al eliminar marca manual en MSSQL para legajo {Legajo}.", legajo);
            return false;
        }
    }

    private static bool TryParseLegajo(object? valor, out int legajo) =>
        int.TryParse(Convert.ToString(valor)?.Trim(), out legajo);

    /// <summary>Resuelve el USERID del legajo y abre conexión. Null si algo falla.</summary>
    private async Task<(SqlConnection Conexion, int UserId)?> ResolverUsuarioAsync(int legajo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.SqlServerConnectionString))
            return null;

        try
        {
            var conexion = new SqlConnection(_options.SqlServerConnectionString);
            await conexion.OpenAsync(ct);

            await using var cmdUser = new SqlCommand("SELECT USERID FROM userinfo WHERE BADGENUMBER = @Legajo", conexion);
            cmdUser.Parameters.Add("@Legajo", SqlDbType.VarChar).Value = legajo.ToString();
            var result = await cmdUser.ExecuteScalarAsync(ct);
            if (result is null || result is DBNull)
            {
                _logger.LogWarning("Reloj: no se encontró USERID para legajo {Legajo} en userinfo.", legajo);
                await conexion.DisposeAsync();
                return null;
            }

            return (conexion, Convert.ToInt32(result));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Reloj: error de conexión para legajo {Legajo}.", legajo);
            return null;
        }
    }

    private static TipoMarca ParseTipo(object? valor)
    {
        var texto = Convert.ToString(valor)?.Trim().ToLowerInvariant() ?? "";
        return texto switch
        {
            "0" or "in" or "entrada" or "e" or "i" => TipoMarca.Entrada,
            "1" or "out" or "salida" or "s" or "o" => TipoMarca.Salida,
            _ => TipoMarca.Entrada,
        };
    }
}

/// <summary>
/// Fuente simulada para desarrollo: genera marcas deterministas (entrada ~08:00, salida ~16-18hs)
/// para los últimos 30 días, con alguna anomalía ocasional. Permite probar el portal sin el reloj real.
/// </summary>
public sealed class MockRelojDataSource : IRelojDataSource
{
    private readonly RelojOptions _options;

    public MockRelojDataSource(IOptions<RelojOptions> options) => _options = options.Value;

    public Task<IReadOnlyList<MarcaRelojCruda>> ObtenerMarcasAsync(int? legajo, DateTime desde, DateTime hasta,
        CancellationToken ct = default)
    {
        var legajos = _options.MockLegajos
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .Where(l => legajo is null || l == legajo)
            .ToList();

        var marcas = new List<MarcaRelojCruda>();
        var hastaDia = DateOnly.FromDateTime(DateTime.Today < hasta ? DateTime.Today : hasta);
        for (var fecha = DateOnly.FromDateTime(desde); fecha <= hastaDia; fecha = fecha.AddDays(1))
        {
            if (fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;

            foreach (var leg in legajos)
            {
                var semilla = leg * 1000 + fecha.DayNumber;
                var random = new Random(semilla);

                var entrada = fecha.ToDateTime(new TimeOnly(8, 0)).AddMinutes(random.Next(-20, 35));
                marcas.Add(new MarcaRelojCruda(leg, entrada, TipoMarca.Entrada, "mock-biometrico-01"));

                // Anomalía ocasional: día sin salida registrada
                if (random.Next(0, 7) != 0)
                {
                    var salida = fecha.ToDateTime(new TimeOnly(16, 30)).AddMinutes(random.Next(0, 90));
                    marcas.Add(new MarcaRelojCruda(leg, salida, TipoMarca.Salida, "mock-biometrico-01"));
                }
            }
        }

        return Task.FromResult<IReadOnlyList<MarcaRelojCruda>>(marcas);
    }

    public Task<bool> RegistrarMarcaAsync(int legajo, DateTime fechaHora, TipoMarca tipo, CancellationToken ct = default)
    {
        // Mock: solo loguea, no hay MSSQL real
        return Task.FromResult(true);
    }

    public Task<bool> EditarMarcaAsync(int legajo, DateTime fechaHoraVieja, TipoMarca tipoViejo,
        DateTime fechaHoraNueva, TipoMarca tipoNuevo, CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }

    public Task<bool> EliminarMarcaAsync(int legajo, DateTime fechaHora, TipoMarca tipo, CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }
}