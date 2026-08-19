using FluentAssertions;
using PortalIUPA.Domain.ValueObjects;
using Xunit;

namespace PortalIUPA.Domain.Tests;

public class RangoFechasTests
{
    [Fact]
    public void Constructor_RechazaFinAnteriorAInicio()
    {
        var act = () => new RangoFechas(new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 5));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Dias_EsInclusivo()
    {
        var rango = new RangoFechas(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 10));

        rango.Dias.Should().Be(10);
    }

    [Theory]
    [InlineData(2026, 8, 5, 2026, 8, 20, true)]
    [InlineData(2026, 8, 1, 2026, 8, 3, true)]
    [InlineData(2026, 9, 1, 2026, 9, 3, false)]
    [InlineData(2026, 8, 5, 2026, 8, 7, true)]
    public void SolapaCon_DetectaSobrelaposicion(int y1, int m1, int d1, int y2, int m2, int d2, bool esperado)
    {
        var rango = new RangoFechas(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 10));

        rango.SolapaCon(new RangoFechas(new DateOnly(y1, m1, d1), new DateOnly(y2, m2, d2))).Should().Be(esperado);
    }

    [Fact]
    public void DiasDentroDelMes_RecortaAlLimiteDelMes()
    {
        var rango = new RangoFechas(new DateOnly(2026, 8, 28), new DateOnly(2026, 9, 2));

        rango.DiasDentroDelMes(2026, 8).Should().Be(4);
        rango.DiasDentroDelMes(2026, 9).Should().Be(2);
    }

    [Fact]
    public void DiasDentroDelAnio_RecortaPorAnio()
    {
        var rango = new RangoFechas(new DateOnly(2026, 12, 30), new DateOnly(2027, 1, 2));

        rango.DiasDentroDelAnio(2026).Should().Be(2);
        rango.DiasDentroDelAnio(2027).Should().Be(2);
    }
}

public class EmailTests
{
    [Fact]
    public void EsInstitucional_DetectaDominioIupa()
    {
        new Email("juan.perez@iupa.edu.ar").EsInstitucional.Should().BeTrue();
        new Email("juan.perez@GMAIL.COM").EsInstitucional.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-un-mail")]
    public void Constructor_RechazaFormatosInvalidos(string valor)
    {
        var act = () => new Email(valor);

        act.Should().Throw<ArgumentException>();
    }
}