using MediatR;
using PortalIUPA.Application.DTOs;
using PortalIUPA.Domain.DomainServices;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Application.UseCases.Estadisticas;

/// <summary>
/// Estadísticas de asistencia del personal calculadas en vivo sobre las marcas
/// del reloj biométrico (SQL Server), no sobre la base local del portal.
/// </summary>
public sealed record GetEstadisticasFichadasQuery(DateOnly Desde, DateOnly Hasta)
    : IRequest<EstadisticasFichadasDto>;

public sealed class GetEstadisticasFichadasQueryHandler : IRequestHandler<GetEstadisticasFichadasQuery,
    EstadisticasFichadasDto>
{
    private static readonly TimeOnly LimiteTardanza = new(8, 30);
    private static readonly string[] Dias =
        ["Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado"];

    private readonly IRelojDataSource _reloj;
    private readonly IEmpleadoRepository _empleados;
    private readonly IAreaRepository _areas;

    public GetEstadisticasFichadasQueryHandler(IRelojDataSource reloj, IEmpleadoRepository empleados,
        IAreaRepository areas)
    {
        _reloj = reloj;
        _empleados = empleados;
        _areas = areas;
    }

    public async Task<EstadisticasFichadasDto> Handle(GetEstadisticasFichadasQuery request, CancellationToken ct)
    {
        var desde = request.Desde.ToDateTime(TimeOnly.MinValue);
        var hasta = request.Hasta.ToDateTime(TimeOnly.MaxValue);

        var marcas = await _reloj.ObtenerMarcasAsync(null, desde, hasta, ct);
        var activos = await _empleados.GetActivosAsync(ct);
        var empleadosPorLegajo = activos.ToDictionary(e => e.Legajo);

        // Consideramos "habituales" a los empleados activos con al menos una marca en el período.
        var habituales = marcas.Select(m => m.Legajo).Distinct().ToHashSet();
        var empleadosArea = activos
            .Where(e => habituales.Contains(e.Legajo))
            .ToDictionary(e => e.Legajo, e => e.AreaId);

        var nombres = empleadosPorLegajo.ToDictionary(
            kv => kv.Key, kv => $"{kv.Value.Apellido}, {kv.Value.Nombre}");

        // Jornadas por empleado
        var jornadasPorEmpleado = new Dictionary<int, IReadOnlyList<Jornada>>();
        foreach (var grupo in marcas.Where(m => empleadosPorLegajo.ContainsKey(m.Legajo)).GroupBy(m => m.Legajo))
            jornadasPorEmpleado[grupo.Key] = CalculadorJornadas.AgruparEnJornadas(grupo);

        var presentesPorDia = marcas
            .GroupBy(m => DateOnly.FromDateTime(m.FechaHora))
            .ToDictionary(g => g.Key, g => g.Select(m => m.Legajo).Where(empleadosPorLegajo.ContainsKey).ToHashSet());

        // Asistencia vs inasistencia por día laborable
        var porDia = new List<AsistenciaDiaDto>();
        var ocurrenciasDiaSemana = new int[7];
        var presentesDiaSemana = new int[7];
        var totalHoras = TimeSpan.Zero;
        var tardanzasPorEmpleado = new Dictionary<int, int>();
        var totalTardanzas = 0;

        for (var fecha = request.Desde; fecha <= request.Hasta; fecha = fecha.AddDays(1))
        {
            if (EsFinDeSemana(fecha)) continue;

            presentesPorDia.TryGetValue(fecha, out var presentes);
            var nPresentes = presentes?.Count ?? 0;
            var nAusentes = habituales.Count - nPresentes;
            porDia.Add(new AsistenciaDiaDto(fecha, Dias[(int)fecha.DayOfWeek], nPresentes, Math.Max(0, nAusentes)));

            ocurrenciasDiaSemana[(int)fecha.DayOfWeek]++;
            presentesDiaSemana[(int)fecha.DayOfWeek] += nPresentes;
        }

        // Tardanzas, horas y ranking por empleado
        foreach (var (legajo, jornadas) in jornadasPorEmpleado)
        {
            var tardanzas = jornadas.Count(j => j.Entrada is { } e && TimeOnly.FromDateTime(e) > LimiteTardanza);
            if (tardanzas > 0) tardanzasPorEmpleado[legajo] = tardanzas;
            totalTardanzas += tardanzas;
            totalHoras += TimeSpan.FromTicks(jornadas.Where(j => j.Horas.HasValue).Sum(j => j.Horas!.Value.Ticks));
        }

        var topEmpleados = jornadasPorEmpleado
            .Select(kv =>
            {
                var resumen = CalculadorJornadas.CalcularResumen(kv.Value);
                return new TopFichadasEmpleadoDto(kv.Key, nombres.GetValueOrDefault(kv.Key, $"Legajo {kv.Key}"),
                    resumen.DiasTrabajados, Math.Round(resumen.TotalHoras.TotalHours, 1),
                    Math.Round((resumen.PromedioHoras ?? TimeSpan.Zero).TotalHours, 1));
            })
            .OrderByDescending(t => t.Horas)
            .Take(10)
            .ToList();

        var topTardanzas = tardanzasPorEmpleado
            .OrderByDescending(kv => kv.Value)
            .Take(10)
            .Select(kv => new TardanzasEmpleadoDto(kv.Key, nombres.GetValueOrDefault(kv.Key, $"Legajo {kv.Key}"),
                kv.Value))
            .ToList();

        // Marcas de entrada/salida por franja horaria (05 a 20)
        var entradasPorHora = marcas
            .Where(m => m.Tipo == TipoMarca.Entrada && empleadosPorLegajo.ContainsKey(m.Legajo))
            .GroupBy(m => m.FechaHora.Hour)
            .ToDictionary(g => g.Key, g => g.Count());
        var salidasPorHora = marcas
            .Where(m => m.Tipo == TipoMarca.Salida && empleadosPorLegajo.ContainsKey(m.Legajo))
            .GroupBy(m => m.FechaHora.Hour)
            .ToDictionary(g => g.Key, g => g.Count());
        var porFranja = Enumerable.Range(5, 16)
            .Select(h => new FranjaFichadasDto($"{h:00}:00", entradasPorHora.GetValueOrDefault(h), salidasPorHora.GetValueOrDefault(h)))
            .ToList();

        // Promedios de ingreso/egreso
        var todasJornadas = jornadasPorEmpleado.Values.SelectMany(x => x).ToList();
        string? horaPromEntrada = null, horaPromSalida = null;
        var entradas = todasJornadas.Where(j => j.Entrada.HasValue).Select(j => j.Entrada!.Value.TimeOfDay).ToList();
        if (entradas.Count > 0)
        {
            var avgTicks = (long)entradas.Average(t => t.Ticks);
            horaPromEntrada = new TimeOnly(avgTicks).ToString("HH:mm");
        }
        var salidas = todasJornadas.Where(j => j.Salida.HasValue).Select(j => j.Salida!.Value.TimeOfDay).ToList();
        if (salidas.Count > 0)
        {
            var avgTicks = (long)salidas.Average(t => t.Ticks);
            horaPromSalida = new TimeOnly(avgTicks).ToString("HH:mm");
        }

        // Concurrencia por día de la semana (promedio de presentes por semana)
        var porDiaSemana = new[] { 1, 2, 3, 4, 5 }
            .Select(d => new DiaSemanaFichadasDto(Dias[d], presentesDiaSemana[d],
                ocurrenciasDiaSemana[d] == 0 ? 0 : Math.Round((double)presentesDiaSemana[d] / ocurrenciasDiaSemana[d], 1)))
            .ToList();

        // Asistencia promedio por área (habituales)
        var nombresArea = (await _areas.GetAllAsync(ct)).ToDictionary(a => a.Id, a => a.Nombre);
        var porArea = habituales
            .GroupBy(l => empleadosArea.GetValueOrDefault(l))
            .Select(g =>
            {
                var nombre = g.Key is { } id ? nombresArea.GetValueOrDefault(id, "Sin área") : "Sin área";
                var diasEmpleado = jornadasPorEmpleado
                    .Where(kv => g.Contains(kv.Key))
                    .Sum(kv => kv.Value.Count);
                var posibles = g.Count() * porDia.Count;
                return new AreaFichadasDto(nombre, g.Count(),
                    posibles == 0 ? 0 : Math.Round(100.0 * diasEmpleado / posibles, 1));
            })
            .OrderByDescending(a => a.AsistenciaPct)
            .ToList();

        // Resumen general
        var totalPresentes = porDia.Sum(d => d.Presentes);
        var promedioPct = porDia.Count == 0 || habituales.Count == 0
            ? 0
            : Math.Round(100.0 * totalPresentes / (porDia.Count * habituales.Count), 1);

        var masConcurrido = porDiaSemana.Where(d => d.TotalPresentes > 0)
            .MaxBy(d => d.Promedio)?.Dia;
        var menosConcurrido = porDiaSemana.Where(d => d.TotalPresentes > 0)
            .MinBy(d => d.Promedio)?.Dia;
        var franjaPico = porFranja.MaxBy(f => f.Entradas) is { Entradas: > 0 } fp ? fp.Franja : null;

        var resumen = new ResumenFichadasDto(habituales.Count, porDia.Count, promedioPct,
            Math.Round(totalHoras.TotalHours, 1), totalTardanzas, masConcurrido, menosConcurrido, franjaPico,
            horaPromEntrada, horaPromSalida);

        return new EstadisticasFichadasDto(request.Desde, request.Hasta, resumen, porDia,
            porDiaSemana, porFranja, topEmpleados, topTardanzas, porArea);
    }

    private static bool EsFinDeSemana(DateOnly fecha) =>
        fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}
