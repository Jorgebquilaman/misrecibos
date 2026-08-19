using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Npgsql;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Infrastructure.External.SiuMapuche;

public sealed class SiuMapucheOptions
{
    public string ConnectionString { get; set; } = string.Empty;
}

/// <summary>
/// Lee los períodos de liquidación desde la base SIU-Mapuche (PostgreSQL), tabla mapuche.dh22.
/// El código del período es "AAAA-MM" para la liquidación mensual habitual y "AAAA-MM-nroliq"
/// para liquidaciones especiales (aguinaldos, complementarias, etc.), evitando colisiones.
/// </summary>
public sealed class SiuMapuchePeriodosDataSource : IPeriodosDataSource
{
    private static readonly Regex Mensual = new("^Liquidaci[oó]n correspondiente al mes de",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly SiuMapucheOptions _opciones;

    public SiuMapuchePeriodosDataSource(IOptions<SiuMapucheOptions> opciones) => _opciones = opciones.Value;

    public async Task<IReadOnlyList<PeriodoExterno>> ObtenerPeriodosAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT d.nro_liqui, d.per_liano, d.per_limes, d.desc_liqui, d.sino_cerra
            FROM mapuche.dh22 d
            ORDER BY d.per_liano DESC, d.per_limes DESC, d.nro_liqui DESC
            """;

        var periodos = new List<PeriodoExterno>();
        await using var conexion = new NpgsqlConnection(_opciones.ConnectionString);
        await conexion.OpenAsync(ct);

        await using var comando = new NpgsqlCommand(sql, conexion);
        await using var lector = await comando.ExecuteReaderAsync(ct);

        while (await lector.ReadAsync(ct))
        {
            var nroLiq = lector.GetInt32(0);
            var anio = lector.GetInt32(1);
            var mes = lector.GetInt32(2);
            var descripcion = lector.IsDBNull(3) ? string.Empty : lector.GetString(3).Trim();
            var cerrada = lector.GetString(4) == "S";

            var codigo = Mensual.IsMatch(descripcion)
                ? $"{anio:D4}-{mes:D2}"
                : $"{anio:D4}-{mes:D2}-{nroLiq}";

            periodos.Add(new PeriodoExterno(nroLiq, codigo, descripcion, cerrada));
        }

        return periodos;
    }
}