using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        var marcas = new List<MarcaRelojCruda>();
        if (string.IsNullOrWhiteSpace(_options.SqlServerConnectionString))
        {
            _logger.LogWarning("Reloj: no hay SqlServerConnectionString configurada. No se leyeron marcas.");
            return marcas;
        }

        try
        {
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
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Reloj: error al leer las marcas del checador.");
        }

        return marcas;
    }

    private static bool TryParseLegajo(object? valor, out int legajo) =>
        int.TryParse(Convert.ToString(valor)?.Trim(), out legajo);

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
}