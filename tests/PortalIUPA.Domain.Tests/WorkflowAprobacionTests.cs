using FluentAssertions;
using PortalIUPA.Domain.DomainServices;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Enums;
using Xunit;

namespace PortalIUPA.Domain.Tests;

public class WorkflowAprobacionTests
{
    private static TipoLicencia CrearTipoConNiveles(params AprobadorRequerido[] niveles)
    {
        var tipo = new TipoLicencia("Licencia Médica");
        foreach (var nivel in niveles)
            tipo.AgregarNivel(nivel);
        return tipo;
    }

    private static Aprobacion Decidir(Guid solicitudId, NivelAprobacion nivel, ResultadoAprobacion resultado) =>
        new(solicitudId, nivel.Id, Guid.NewGuid(), resultado, null);

    [Fact]
    public void SiguienteNivelPendiente_DevuelvePrimerNivel_CuandoNoHayDecisiones()
    {
        var tipo = CrearTipoConNiveles(AprobadorRequerido.ResponsableDirecto, AprobadorRequerido.Rrhh);

        var pendiente = WorkflowAprobacion.SiguienteNivelPendiente(tipo, Array.Empty<Aprobacion>());

        pendiente.Should().NotBeNull();
        pendiente!.Orden.Should().Be(1);
        pendiente.RolRequerido.Should().Be(AprobadorRequerido.ResponsableDirecto);
    }

    [Fact]
    public void SiguienteNivelPendiente_DevuelveSegundoNivel_CuandoElPrimeroFueAprobado()
    {
        var tipo = CrearTipoConNiveles(AprobadorRequerido.ResponsableDirecto, AprobadorRequerido.Rrhh);
        var solicitudId = Guid.NewGuid();
        var primerNivel = tipo.NivelesOrdenados[0];
        var decisiones = new[] { Decidir(solicitudId, primerNivel, ResultadoAprobacion.Aprobado) };

        var pendiente = WorkflowAprobacion.SiguienteNivelPendiente(tipo, decisiones);

        pendiente.Should().NotBeNull();
        pendiente!.Orden.Should().Be(2);
        pendiente.RolRequerido.Should().Be(AprobadorRequerido.Rrhh);
    }

    [Fact]
    public void SiguienteNivelPendiente_DevuelveNull_CuandoLaCadenaEstaCompleta()
    {
        var tipo = CrearTipoConNiveles(AprobadorRequerido.ResponsableDirecto, AprobadorRequerido.Rrhh);
        var solicitudId = Guid.NewGuid();
        var decisiones = tipo.NivelesOrdenados
            .Select(n => Decidir(solicitudId, n, ResultadoAprobacion.Aprobado))
            .ToArray();

        var pendiente = WorkflowAprobacion.SiguienteNivelPendiente(tipo, decisiones);

        pendiente.Should().BeNull();
    }

    [Fact]
    public void SiguienteNivelPendiente_DevuelveNull_CuandoUnNivelFueRechazado()
    {
        var tipo = CrearTipoConNiveles(AprobadorRequerido.ResponsableDirecto, AprobadorRequerido.Rrhh);
        var solicitudId = Guid.NewGuid();
        var primerNivel = tipo.NivelesOrdenados[0];
        var decisiones = new[] { Decidir(solicitudId, primerNivel, ResultadoAprobacion.Rechazado) };

        var pendiente = WorkflowAprobacion.SiguienteNivelPendiente(tipo, decisiones);

        pendiente.Should().BeNull();
    }

    [Fact]
    public void CalcularEstado_EsDesaprobada_SiAlgunNivelFueRechazado()
    {
        var tipo = CrearTipoConNiveles(AprobadorRequerido.ResponsableDirecto, AprobadorRequerido.Rrhh);
        var solicitudId = Guid.NewGuid();
        var decisiones = new[]
        {
            Decidir(solicitudId, tipo.NivelesOrdenados[0], ResultadoAprobacion.Aprobado),
            Decidir(solicitudId, tipo.NivelesOrdenados[1], ResultadoAprobacion.Rechazado),
        };

        var estado = WorkflowAprobacion.CalcularEstado(tipo, decisiones);

        estado.Should().Be(EstadoSolicitud.Desaprobada);
    }

    [Fact]
    public void CalcularEstado_EsAprobada_CuandoLaCadenaEstaCompleta()
    {
        var tipo = CrearTipoConNiveles(AprobadorRequerido.ResponsableDirecto, AprobadorRequerido.Rrhh);
        var solicitudId = Guid.NewGuid();
        var decisiones = tipo.NivelesOrdenados
            .Select(n => Decidir(solicitudId, n, ResultadoAprobacion.Aprobado))
            .ToArray();

        var estado = WorkflowAprobacion.CalcularEstado(tipo, decisiones);

        estado.Should().Be(EstadoSolicitud.Aprobada);
    }

    [Fact]
    public void CalcularEstado_EsEnEspera_MientrasHayaNivelesSinDecidir()
    {
        var tipo = CrearTipoConNiveles(AprobadorRequerido.ResponsableDirecto, AprobadorRequerido.Rrhh);
        var solicitudId = Guid.NewGuid();
        var decisiones = new[]
        {
            Decidir(solicitudId, tipo.NivelesOrdenados[0], ResultadoAprobacion.Aprobado),
        };

        var estado = WorkflowAprobacion.CalcularEstado(tipo, decisiones);

        estado.Should().Be(EstadoSolicitud.EnEspera);
    }

    [Fact]
    public void CalcularEstado_EsAprobada_SiElTipoNoTieneNiveles()
    {
        var tipo = new TipoLicencia("Franco");

        var estado = WorkflowAprobacion.CalcularEstado(tipo, Array.Empty<Aprobacion>());

        estado.Should().Be(EstadoSolicitud.Aprobada);
    }

    [Fact]
    public void TieneElRolRequerido_EsFalso_ParaResponsableDirecto()
    {
        var empleado = new Empleado(1, "Ana", "García", new("ana@iupa.edu.ar"));

        var resultado = WorkflowAprobacion.TieneElRolRequerido(empleado, AprobadorRequerido.ResponsableDirecto);

        resultado.Should().BeFalse();
    }

    [Fact]
    public void TieneElRolRequerido_EsVerdadero_SegunMembresiaDeRol()
    {
        var rrhh = new Empleado(2, "Ramiro", "Pérez", new("rrhh@iupa.edu.ar"));
        rrhh.AgregarRol(Rol.Rrhh);

        WorkflowAprobacion.TieneElRolRequerido(rrhh, AprobadorRequerido.Rrhh).Should().BeTrue();
        WorkflowAprobacion.TieneElRolRequerido(rrhh, AprobadorRequerido.Direccion).Should().BeFalse();
    }
}