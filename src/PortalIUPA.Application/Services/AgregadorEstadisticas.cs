using System.Globalization;
using PortalIUPA.Application.DTOs;
using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Application.Services;

/// <summary>Agregaciones para el reporte de accesos (semana ISO y tipo de acción).</summary>
public static class AgregadorEstadisticas
{
    public static IReadOnlyList<AccesosPorSemanaDto> PorSemana(IEnumerable<AccesoLog> logs)
    {
        var calendario = CultureInfo.InvariantCulture.Calendar;
        return logs
            .GroupBy(l =>
            {
                var fecha = l.FechaHora.ToLocalTime().Date;
                var semana = calendario.GetWeekOfYear(fecha, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                return (fecha.Year, semana);
            })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.semana)
            .Select(g =>
            {
                var inicio = ISOInicioDeSemana(g.Key.Year, g.Key.semana);
                return new AccesosPorSemanaDto($"Semana {g.Key.semana} de {g.Key.Year}", inicio, inicio.AddDays(6),
                    g.Count());
            })
            .ToList();
    }

    public static IReadOnlyList<AccesosPorAccionDto> PorAccion(IEnumerable<AccesoLog> logs) =>
        logs
            .GroupBy(l => l.Accion)
            .OrderByDescending(g => g.Count())
            .Select(g => new AccesosPorAccionDto(g.Key, g.Count()))
            .ToList();

    private static DateOnly ISOInicioDeSemana(int anio, int semana)
    {
        var primerDia = new DateTime(anio, 1, 1);
        var diasHastaLunes = ((int)DayOfWeek.Monday - (int)primerDia.DayOfWeek + 7) % 7;
        var primerLunes = primerDia.AddDays(diasHastaLunes);
        return DateOnly.FromDateTime(primerLunes.AddDays((semana - 1) * 7));
    }
}