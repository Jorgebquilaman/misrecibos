using FluentAssertions;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.ValueObjects;
using Xunit;

namespace PortalIUPA.Domain.Tests;

public class SolicitudLicenciaTests
{
    private static Empleado CrearEmpleado(int legajo = 1) =>
        new(legajo, "Ana", "García", new Email("ana@iupa.edu.ar"));

    [Fact]
    public void Constructor_CreaSolicitudEnEspera()
    {
        var solicitud = new SolicitudLicencia(Guid.NewGuid(), Guid.NewGuid(),
            new RangoFechas(new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12)), "Asunto", "Motivo");

        solicitud.Estado.Should().Be(EstadoSolicitud.EnEspera);
        solicitud.Dias.Should().Be(3);
    }

    [Fact]
    public void SolapaCon_DetectaSobreposicion()
    {
        var solicitud = new SolicitudLicencia(Guid.NewGuid(), Guid.NewGuid(),
            new RangoFechas(new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15)));

        solicitud.SolapaCon(new RangoFechas(new DateOnly(2026, 8, 14), new DateOnly(2026, 8, 20)))
            .Should().BeTrue();
        solicitud.SolapaCon(new RangoFechas(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 5)))
            .Should().BeFalse();
    }

    [Fact]
    public void Cancelar_MarcaCancelada()
    {
        var solicitud = new SolicitudLicencia(Guid.NewGuid(), Guid.NewGuid(),
            new RangoFechas(new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15)));

        solicitud.Cancelar();

        solicitud.Estado.Should().Be(EstadoSolicitud.Cancelada);
    }

    [Fact]
    public void Cancelar_LanzaSiYaFueResuelta()
    {
        var solicitud = new SolicitudLicencia(Guid.NewGuid(), Guid.NewGuid(),
            new RangoFechas(new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 15)));
        solicitud.ActualizarEstado(EstadoSolicitud.Aprobada);

        var act = () => solicitud.Cancelar();

        act.Should().Throw<InvalidOperationException>();
    }
}

public class EmpleadoTests
{
    [Fact]
    public void Constructor_SiempreTieneRolBaseEmpleado()
    {
        var empleado = new Empleado(100, "Ana", "García", new Email("ana@iupa.edu.ar"));

        empleado.TieneRol(Rol.Empleado).Should().BeTrue();
        empleado.NombreCompleto.Should().Be("García, Ana");
    }

    [Fact]
    public void AgregarRol_NoDuplicaRoles()
    {
        var empleado = new Empleado(100, "Ana", "García", new Email("ana@iupa.edu.ar"));

        empleado.AgregarRol(Rol.Rrhh);
        empleado.AgregarRol(Rol.Rrhh);

        empleado.Roles.Should().ContainSingle(r => r == Rol.Rrhh);
    }

    [Fact]
    public void QuitarRol_NoPermiteQuitarElBase()
    {
        var empleado = new Empleado(100, "Ana", "García", new Email("ana@iupa.edu.ar"));

        var act = () => empleado.QuitarRol(Rol.Empleado);

        act.Should().Throw<InvalidOperationException>();
    }
}

public class TipoLicenciaTests
{
    [Fact]
    public void AgregarNivel_AsignaOrdenesSecuenciales()
    {
        var tipo = new TipoLicencia("Médica");

        tipo.AgregarNivel(AprobadorRequerido.ResponsableDirecto);
        tipo.AgregarNivel(AprobadorRequerido.Rrhh);
        tipo.AgregarNivel(AprobadorRequerido.Direccion);

        tipo.NivelesOrdenados.Select(n => n.Orden).Should().Equal(1, 2, 3);
        tipo.NivelesOrdenados.Select(n => n.RolRequerido)
            .Should().Equal(AprobadorRequerido.ResponsableDirecto, AprobadorRequerido.Rrhh, AprobadorRequerido.Direccion);
    }

    [Fact]
    public void QuitarNivel_ReordenaLaCadena()
    {
        var tipo = new TipoLicencia("Médica");
        tipo.AgregarNivel(AprobadorRequerido.ResponsableDirecto);
        tipo.AgregarNivel(AprobadorRequerido.Rrhh);
        var aEliminar = tipo.Niveles[0];

        tipo.QuitarNivel(aEliminar.Id);

        tipo.NivelesOrdenados.Should().ContainSingle().Which.Orden.Should().Be(1);
    }
}

public class AnuncioTests
{
    private static Empleado CrearEmpleado() =>
        new(1, "Ana", "García", new Email("ana@iupa.edu.ar"));

    [Fact]
    public void EstaVigente_RespetaRangoYEstado()
    {
        var anuncio = new Anuncio("Título", "Cuerpo", Guid.NewGuid(),
            fechaDesde: new DateOnly(2026, 8, 1), fechaHasta: new DateOnly(2026, 8, 31));

        anuncio.EstaVigente(new DateOnly(2026, 8, 15)).Should().BeTrue();
        anuncio.EstaVigente(new DateOnly(2026, 9, 1)).Should().BeFalse();
        anuncio.Desactivar();
        anuncio.EstaVigente(new DateOnly(2026, 8, 15)).Should().BeFalse();
    }

    [Fact]
    public void AplicaA_RespetaElAlcance()
    {
        var areaId = Guid.NewGuid();
        var empleadoEnArea = CrearEmpleado();
        empleadoEnArea.AsignarArea(areaId);

        var deArea = new Anuncio("T", "C", Guid.NewGuid(), alcance: AlcanceAnuncio.Area, areaId: areaId);
        var porRol = new Anuncio("T", "C", Guid.NewGuid(), alcance: AlcanceAnuncio.Rol, rol: Rol.Rrhh);
        var paraTodos = new Anuncio("T", "C", Guid.NewGuid(), alcance: AlcanceAnuncio.Todos);

        deArea.AplicaA(empleadoEnArea).Should().BeTrue();
        porRol.AplicaA(empleadoEnArea).Should().BeFalse();
        paraTodos.AplicaA(empleadoEnArea).Should().BeTrue();
    }
}