using FluentAssertions;
using PortalIUPA.Domain.DomainServices;
using PortalIUPA.Domain.Entities;
using Xunit;

namespace PortalIUPA.Domain.Tests;

public class ValidadorLimitesLicenciaTests
{
    private const int LimiteMensual = 5;
    private const int LimiteAnual = 20;

    [Fact]
    public void Validar_Ok_SiNoSuperaLosLimites()
    {
        var tipo = new TipoLicencia("Particular", LimiteMensual, LimiteAnual);

        var resultado = ValidadorLimitesLicencia.Validar(tipo, 2, 2, 2, 10);

        resultado.EsValido.Should().BeTrue();
    }

    [Fact]
    public void Validar_Falla_SiSuperaElLimiteMensual()
    {
        var tipo = new TipoLicencia("Particular", LimiteMensual, LimiteAnual);

        var resultado = ValidadorLimitesLicencia.Validar(tipo, 4, 4, 3, 10);

        resultado.EsValido.Should().BeFalse();
        resultado.Error.Should().Contain("mensual");
    }

    [Fact]
    public void Validar_Falla_SiSuperaElLimiteAnual()
    {
        var tipo = new TipoLicencia("Particular", LimiteMensual, LimiteAnual);

        var resultado = ValidadorLimitesLicencia.Validar(tipo, 2, 15, 0, 10);

        resultado.EsValido.Should().BeFalse();
        resultado.Error.Should().Contain("anual");
    }

    [Fact]
    public void Validar_Ok_SiElTipoNoTieneLimites()
    {
        var tipo = new TipoLicencia("Sin límites");

        var resultado = ValidadorLimitesLicencia.Validar(tipo, 30, 30, 30, 300);

        resultado.EsValido.Should().BeTrue();
    }

    [Fact]
    public void Validar_Falla_SiElRangoEsInvalido()
    {
        var tipo = new TipoLicencia("Particular", LimiteMensual, LimiteAnual);

        var resultado = ValidadorLimitesLicencia.Validar(tipo, 0, 0, 0, 0);

        resultado.EsValido.Should().BeFalse();
    }
}