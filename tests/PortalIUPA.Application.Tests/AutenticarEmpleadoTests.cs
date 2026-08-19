using FluentAssertions;
using Moq;
using PortalIUPA.Application.Common;
using PortalIUPA.Application.Services;
using PortalIUPA.Application.UseCases.Auth;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.Ports;
using PortalIUPA.Domain.ValueObjects;
using Xunit;

namespace PortalIUPA.Application.Tests;

public class AutenticarEmpleadoTests
{
    private readonly Mock<IEmpleadoRepository> _empleados = new();
    private readonly Mock<IAccesoLogRepository> _accesos = new();
    private readonly Mock<ITokenService> _tokens = new();

    private AutenticarEmpleadoCommandHandler CrearHandler() =>
        new(_empleados.Object, _accesos.Object, _tokens.Object);

    [Fact]
    public async Task RechazaDominioNoInstitucional_YRegistraElIntento()
    {
        var handler = CrearHandler();

        var act = async () => await handler.Handle(
            new AutenticarEmpleadoCommand("juan@gmail.com", "1.2.3.4", "Chrome"), CancellationToken.None);

        await act.Should().ThrowAsync<AccesoDenegadoException>();
        _accesos.Verify(a => a.AddAsync(
            It.Is<AccesoLog>(l => l.Accion == "LoginDominioRechazado" && l.Correo == "juan@gmail.com"),
            It.IsAny<CancellationToken>()), Times.Once);
        _tokens.Verify(t => t.GenerarToken(It.IsAny<Empleado>()), Times.Never);
    }

    [Fact]
    public async Task RechazaEmpleadoNoRegistrado_YRegistraElIntento()
    {
        var handler = CrearHandler();

        var act = async () => await handler.Handle(
            new AutenticarEmpleadoCommand("desconocido@iupa.edu.ar", null, null), CancellationToken.None);

        await act.Should().ThrowAsync<AccesoDenegadoException>();
        _accesos.Verify(a => a.AddAsync(
            It.Is<AccesoLog>(l => l.Accion == "LoginEmpleadoNoRegistrado" &&
                                  l.Correo == "desconocido@iupa.edu.ar"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RechazaEmpleadoDesactivado()
    {
        var empleado = new Empleado(10, "Ana", "García", new Email("ana@iupa.edu.ar"));
        empleado.Desactivar();
        _empleados.Setup(r => r.GetByCorreoAsync("ana@iupa.edu.ar", It.IsAny<CancellationToken>()))
            .ReturnsAsync(empleado);
        var handler = CrearHandler();

        var act = async () => await handler.Handle(
            new AutenticarEmpleadoCommand("ana@iupa.edu.ar", null, null), CancellationToken.None);

        await act.Should().ThrowAsync<AccesoDenegadoException>();
    }

    [Fact]
    public async Task AutenticaEmpleadoVálido_EmiteToken_YRegistraLogin()
    {
        var empleado = new Empleado(10, "Ana", "García", new Email("ana@iupa.edu.ar"));
        empleado.AgregarRol(Rol.Rrhh);
        _empleados.Setup(r => r.GetByCorreoAsync("ana@iupa.edu.ar", It.IsAny<CancellationToken>()))
            .ReturnsAsync(empleado);
        _tokens.Setup(t => t.GenerarToken(empleado)).Returns("jwt-ejemplo");
        var handler = CrearHandler();

        var respuesta = await handler.Handle(
            new AutenticarEmpleadoCommand("ana@iupa.edu.ar", "10.0.0.1", "iPhone"), CancellationToken.None);

        respuesta.Token.Should().Be("jwt-ejemplo");
        respuesta.Roles.Should().Contain(Rol.Rrhh);
        respuesta.EmpleadoId.Should().Be(empleado.Id);
        _accesos.Verify(a => a.AddAsync(
            It.Is<AccesoLog>(l => l.Accion == "Login" && l.EmpleadoId == empleado.Id && l.Ip == "10.0.0.1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}