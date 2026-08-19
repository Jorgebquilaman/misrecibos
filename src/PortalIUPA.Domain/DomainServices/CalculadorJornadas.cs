using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Domain.DomainServices;

public sealed record Jornada(
    DateOnly Fecha,
    DateTime? Entrada,
    DateTime? Salida,
    IReadOnlyList<MarcaRelojCruda> Marcas)
{
    public TimeSpan? Horas => Entrada is { } e && Salida is { } s ? s - e : null;

    /// <summary>Entrada sin salida (o viceversa) o más de 4 marcas en el día se consideran anomalías.</summary>
    public bool EsAnomalia => Entrada is null || Salida is null || Marcas.Count > 4;

    public string? Origen => Marcas.FirstOrDefault()?.Origen;
}

public sealed record ResumenJornadas(
    int DiasTrabajados,
    int DiasConAnomalia,
    TimeSpan TotalHoras)
{
    public TimeSpan? PromedioHoras => DiasTrabajados == 0 ? null : TimeSpan.FromTicks(TotalHoras.Ticks / DiasTrabajados);
}

/// <summary>Agrupa marcas de reloj en jornadas (entrada mínima / salida máxima por día) y calcula resúmenes.</summary>
public static class CalculadorJornadas
{
    public static IReadOnlyList<Jornada> AgruparEnJornadas(IEnumerable<MarcaRelojCruda> marcas)
    {
        ArgumentNullException.ThrowIfNull(marcas);

        return marcas
            .GroupBy(m => DateOnly.FromDateTime(m.FechaHora))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var delDia = g.OrderBy(m => m.FechaHora).ToList();
                var entrada = delDia.Where(m => m.Tipo == TipoMarca.Entrada).Select(m => (DateTime?)m.FechaHora).FirstOrDefault();
                var salida = delDia.Where(m => m.Tipo == TipoMarca.Salida).Select(m => (DateTime?)m.FechaHora).LastOrDefault();
                return new Jornada(g.Key, entrada, salida, delDia);
            })
            .ToList();
    }

    public static ResumenJornadas CalcularResumen(IEnumerable<Jornada> jornadas)
    {
        var lista = jornadas.ToList();
        var horas = lista.Where(j => j.Horas.HasValue).Sum(j => j.Horas!.Value.Ticks);
        return new ResumenJornadas(
            lista.Count,
            lista.Count(j => j.EsAnomalia),
            TimeSpan.FromTicks(horas));
    }
}