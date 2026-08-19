using FluentAssertions;
using PortalIUPA.Domain.DomainServices;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.Ports;
using Xunit;

namespace PortalIUPA.Domain.Tests;

public class CalculadorJornadasTests
{
    private static MarcaRelojCruda Marca(int legajo, int dia, int hora, TipoMarca tipo) =>
        new(legajo, new DateTime(2026, 8, dia, hora, 0, 0), tipo, "checador-01");

    [Fact]
    public void AgruparEnJornadas_ArmaJornadasConEntradaMinimaYSalidaMaxima()
    {
        const int legajo = 123;
        var marcas = new[]
        {
            Marca(legajo, 1, 8, TipoMarca.Entrada),
            Marca(legajo, 1, 12, TipoMarca.Entrada),
            Marca(legajo, 1, 17, TipoMarca.Salida),
            Marca(legajo, 1, 18, TipoMarca.Salida),
            Marca(legajo, 2, 8, TipoMarca.Entrada),
            Marca(legajo, 2, 17, TipoMarca.Salida),
        };

        var jornadas = CalculadorJornadas.AgruparEnJornadas(marcas);

        jornadas.Should().HaveCount(2);
        jornadas[0].Fecha.Should().Be(new DateOnly(2026, 8, 1));
        jornadas[0].Entrada!.Value.Hour.Should().Be(8);
        jornadas[0].Salida!.Value.Hour.Should().Be(18);
        jornadas[0].Horas.Should().Be(TimeSpan.FromHours(10));
        jornadas[1].Horas.Should().Be(TimeSpan.FromHours(9));
        jornadas.Should().OnlyContain(j => !j.EsAnomalia);
    }

    [Fact]
    public void AgruparEnJornadas_MarcaSueltaEsAnomalia()
    {
        const int legajo = 123;
        var marcas = new[]
        {
            Marca(legajo, 1, 8, TipoMarca.Entrada),
            Marca(legajo, 1, 9, TipoMarca.Entrada),
            Marca(legajo, 1, 10, TipoMarca.Entrada),
        };

        var jornadas = CalculadorJornadas.AgruparEnJornadas(marcas);

        jornadas.Should().HaveCount(1);
        jornadas[0].EsAnomalia.Should().BeTrue();
        jornadas[0].Horas.Should().BeNull();
    }

    [Fact]
    public void CalcularResumen_SumaHorasYCuentaAnomalias()
    {
        const int legajo = 123;
        var marcas = new[]
        {
            Marca(legajo, 1, 8, TipoMarca.Entrada),
            Marca(legajo, 1, 16, TipoMarca.Salida),
            Marca(legajo, 2, 8, TipoMarca.Entrada),
            Marca(legajo, 2, 17, TipoMarca.Salida),
            Marca(legajo, 3, 9, TipoMarca.Entrada),
        };

        var resumen = CalculadorJornadas.CalcularResumen(CalculadorJornadas.AgruparEnJornadas(marcas));

        resumen.DiasTrabajados.Should().Be(3);
        resumen.DiasConAnomalia.Should().Be(1);
        resumen.TotalHoras.Should().Be(TimeSpan.FromHours(17));
        resumen.PromedioHoras.Should().Be(TimeSpan.FromHours(5).Add(TimeSpan.FromMinutes(40)));
    }
}