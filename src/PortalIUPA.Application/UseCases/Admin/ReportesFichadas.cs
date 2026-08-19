using MediatR;
using PortalIUPA.Application.Common;
using PortalIUPA.Application.DTOs;
using PortalIUPA.Domain.DomainServices;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Application.UseCases.Admin;

/// <summary>
/// Genera reportes de fichadas leyendo las marcas en vivo del reloj biométrico.
/// Tipos: "individual" (un empleado, día por día), "resumen" (toda la institución),
/// "ausencias" (quiénes no marcaron cada día) y "tardanzas" (entradas posteriores a 08:30).
/// </summary>
public sealed record GenerarReporteFichadasQuery(string Tipo, int? Legajo, DateOnly Desde, DateOnly Hasta)
    : IRequest<ReporteTabularDto>;

public sealed class GenerarReporteFichadasQueryHandler : IRequestHandler<GenerarReporteFichadasQuery, ReporteTabularDto>
{
    private static readonly TimeOnly LimiteTardanza = new(8, 30);
    private static readonly string[] Dias =
        ["Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado"];

    private readonly IRelojDataSource _reloj;
    private readonly IEmpleadoRepository _empleados;

    public GenerarReporteFichadasQueryHandler(IRelojDataSource reloj, IEmpleadoRepository empleados)
    {
        _reloj = reloj;
        _empleados = empleados;
    }

    public async Task<ReporteTabularDto> Handle(GenerarReporteFichadasQuery request, CancellationToken ct)
    {
        var desde = request.Desde.ToDateTime(TimeOnly.MinValue);
        var hasta = request.Hasta.ToDateTime(TimeOnly.MaxValue);

        return request.Tipo.ToLowerInvariant() switch
        {
            "individual" => await IndividualAsync(request.Legajo, request.Desde, request.Hasta, desde, hasta, ct),
            "resumen" => await ResumenAsync(request.Desde, request.Hasta, desde, hasta, ct),
            "ausencias" => await AusenciasAsync(request.Desde, request.Hasta, desde, hasta, ct),
            "tardanzas" => await TardanzasAsync(request.Desde, request.Hasta, desde, hasta, ct),
            _ => throw new ReglaDeNegocioException(
                "Tipo de reporte no válido. Usá individual, resumen, ausencias o tardanzas."),
        };
    }

    private async Task<ReporteTabularDto> IndividualAsync(int? legajo, DateOnly desdeD, DateOnly hastaD,
        DateTime desde, DateTime hasta, CancellationToken ct)
    {
        if (legajo is null)
            throw new ReglaDeNegocioException("Para el reporte individual indicá el legajo del empleado.");

        var empleado = await _empleados.GetByLegajoAsync(legajo.Value, ct)
            ?? throw new EntidadNoEncontradaException($"No existe un empleado con legajo {legajo}.");

        var marcas = await _reloj.ObtenerMarcasAsync(empleado.Legajo, desde, hasta, ct);
        var jornadas = CalculadorJornadas.AgruparEnJornadas(marcas).ToDictionary(j => j.Fecha);

        var filas = new List<IReadOnlyList<string>>();
        var diasConMarca = 0;
        var totalHoras = TimeSpan.Zero;
        var anomalias = 0;
        var tardanzas = 0;

        for (var fecha = desdeD; fecha <= hastaD; fecha = fecha.AddDays(1))
        {
            if (EsFinDeSemana(fecha)) continue;

            if (jornadas.TryGetValue(fecha, out var j))
            {
                diasConMarca++;
                if (j.Horas is { } h) totalHoras += h;
                if (j.EsAnomalia) anomalias++;
                var conTardanza = j.Entrada is { } e && TimeOnly.FromDateTime(e) > LimiteTardanza;
                if (conTardanza) tardanzas++;

                filas.Add([fecha.ToString("dd/MM/yyyy"), Dias[(int)fecha.DayOfWeek],
                    j.Entrada?.ToString("HH:mm") ?? "-", j.Salida?.ToString("HH:mm") ?? "-",
                    j.Horas is { } hh ? FormatearHoras(hh) : "-",
                    conTardanza ? "Sí" : "-", j.EsAnomalia ? "Sí" : "-"]);
            }
            else
            {
                filas.Add([fecha.ToString("dd/MM/yyyy"), Dias[(int)fecha.DayOfWeek], "-", "-", "-", "-", "Sin marcas"]);
            }
        }

        var resumen = $"Días con marcas: {diasConMarca} · Horas totales: {FormatearHoras(totalHoras)} · " +
                      $"Anomalías: {anomalias} · Tardanzas (> {LimiteTardanza:HH\\:mm}): {tardanzas}";

        return new ReporteTabularDto("Reporte de fichadas individual",
            $"{empleado.Apellido}, {empleado.Nombre} (legajo {empleado.Legajo}) · del {desdeD:dd/MM/yyyy} al {hastaD:dd/MM/yyyy}",
            ["Fecha", "Día", "Entrada", "Salida", "Horas", "Tardanza", "Anomalía"], filas, resumen);
    }

    private async Task<ReporteTabularDto> ResumenAsync(DateOnly desdeD, DateOnly hastaD,
        DateTime desde, DateTime hasta, CancellationToken ct)
    {
        var marcas = await _reloj.ObtenerMarcasAsync(null, desde, hasta, ct);
        var empleadosPorLegajo = (await _empleados.GetActivosAsync(ct)).ToDictionary(e => e.Legajo);

        var datos = new List<(int Legajo, string Nombre, ResumenJornadas Resumen)>();
        foreach (var grupo in marcas.GroupBy(m => m.Legajo))
        {
            if (!empleadosPorLegajo.TryGetValue(grupo.Key, out var empleado)) continue;
            var resumenEmp = CalculadorJornadas.CalcularResumen(CalculadorJornadas.AgruparEnJornadas(grupo));
            datos.Add((grupo.Key, $"{empleado.Apellido}, {empleado.Nombre}", resumenEmp));
        }

        var filas = datos.OrderByDescending(d => d.Resumen.TotalHoras).Select(d => (IReadOnlyList<string>)new string[]
        {
            d.Legajo.ToString(), d.Nombre, d.Resumen.DiasTrabajados.ToString(),
            FormatearHoras(d.Resumen.TotalHoras),
            d.Resumen.PromedioHoras is { } p ? FormatearHoras(p) : "-",
            d.Resumen.DiasConAnomalia.ToString()
        }).ToList();

        var totalHoras = datos.Sum(d => d.Resumen.TotalHoras.Ticks);
        var laborables = ContarLaborables(desdeD, hastaD);
        var resumen = $"Empleados con presencia: {filas.Count} · Días laborables: {laborables} · " +
                      $"Total horas institución: {FormatearHoras(TimeSpan.FromTicks(totalHoras))}";

        return new ReporteTabularDto("Reporte resumen de fichadas",
            $"Toda la institución · del {desdeD:dd/MM/yyyy} al {hastaD:dd/MM/yyyy}",
            ["Legajo", "Empleado", "Días trabajados", "Horas totales", "Promedio diario", "Anomalías"],
            filas, resumen);
    }

    private async Task<ReporteTabularDto> AusenciasAsync(DateOnly desdeD, DateOnly hastaD,
        DateTime desde, DateTime hasta, CancellationToken ct)
    {
        var marcas = await _reloj.ObtenerMarcasAsync(null, desde, hasta, ct);
        var empleadosPorLegajo = (await _empleados.GetActivosAsync(ct)).ToDictionary(e => e.Legajo);

        // Habituales: empleados que marcaron al menos una vez en el período
        var habituales = marcas.GroupBy(m => m.Legajo)
            .Where(g => empleadosPorLegajo.ContainsKey(g.Key))
            .Select(g => g.Key)
            .ToHashSet();

        var presentesPorDia = marcas
            .GroupBy(m => DateOnly.FromDateTime(m.FechaHora))
            .ToDictionary(g => g.Key, g => g.Select(m => m.Legajo).ToHashSet());

        var filas = new List<IReadOnlyList<string>>();
        var totalAusencias = 0;
        for (var fecha = desdeD; fecha <= hastaD; fecha = fecha.AddDays(1))
        {
            if (EsFinDeSemana(fecha)) continue;

            presentesPorDia.TryGetValue(fecha, out var presentes);
            var ausentes = habituales.Where(l => presentes is null || !presentes.Contains(l))
                .OrderBy(l => l).ToList();
            totalAusencias += ausentes.Count;

            foreach (var legajo in ausentes)
            {
                var empleado = empleadosPorLegajo[legajo];
                filas.Add([fecha.ToString("dd/MM/yyyy"), Dias[(int)fecha.DayOfWeek], legajo.ToString(),
                    $"{empleado.Apellido}, {empleado.Nombre}"]);
            }
        }

        filas.Sort((a, b) => string.CompareOrdinal(a[0], b[0]));
        var resumen = $"Ausencias registradas: {totalAusencias} · Empleados considerados: {habituales.Count}";

        return new ReporteTabularDto("Reporte de ausencias",
            $"Quiénes no marcaron cada día laborable · del {desdeD:dd/MM/yyyy} al {hastaD:dd/MM/yyyy}",
            ["Fecha", "Día", "Legajo", "Empleado"], filas, resumen);
    }

    private async Task<ReporteTabularDto> TardanzasAsync(DateOnly desdeD, DateOnly hastaD,
        DateTime desde, DateTime hasta, CancellationToken ct)
    {
        var marcas = await _reloj.ObtenerMarcasAsync(null, desde, hasta, ct);
        var empleadosPorLegajo = (await _empleados.GetActivosAsync(ct)).ToDictionary(e => e.Legajo);

        var filas = new List<IReadOnlyList<string>>();
        var totalTardanzas = 0;
        foreach (var grupo in marcas.GroupBy(m => m.Legajo))
        {
            if (!empleadosPorLegajo.TryGetValue(grupo.Key, out var empleado)) continue;

            foreach (var jornada in CalculadorJornadas.AgruparEnJornadas(grupo))
            {
                if (jornada.Entrada is not { } entrada) continue;
                if (TimeOnly.FromDateTime(entrada) <= LimiteTardanza) continue;

                var retraso = entrada - (entrada.Date + LimiteTardanza.ToTimeSpan());
                filas.Add([jornada.Fecha.ToString("dd/MM/yyyy"), Dias[(int)jornada.Fecha.DayOfWeek],
                    grupo.Key.ToString(), $"{empleado.Apellido}, {empleado.Nombre}",
                    entrada.ToString("HH:mm"), $"{(int)retraso.TotalMinutes} min"]);
                totalTardanzas++;
            }
        }

        filas.Sort((a, b) => string.CompareOrdinal(a[0], b[0]));
        var resumen = $"Tardanzas registradas (entrada posterior a {LimiteTardanza:HH\\:mm}): {totalTardanzas}";

        return new ReporteTabularDto("Reporte de tardanzas",
            $"Entradas posteriores a {LimiteTardanza:HH\\:mm} · del {desdeD:dd/MM/yyyy} al {hastaD:dd/MM/yyyy}",
            ["Fecha", "Día", "Legajo", "Empleado", "Entrada", "Retraso"], filas, resumen);
    }

    private static bool EsFinDeSemana(DateOnly fecha) =>
        fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    private static int ContarLaborables(DateOnly desde, DateOnly hasta)
    {
        var contador = 0;
        for (var f = desde; f <= hasta; f = f.AddDays(1))
            if (!EsFinDeSemana(f)) contador++;
        return contador;
    }

    private static string FormatearHoras(TimeSpan t) => $"{(int)t.TotalHours}:{t.Minutes:00}";
}