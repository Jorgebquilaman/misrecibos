using FluentAssertions;
using Moq;
using PortalIUPA.Application.Common;
using PortalIUPA.Application.Services;
using PortalIUPA.Application.UseCases.Licencias;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.Ports;
using PortalIUPA.Domain.ValueObjects;
using Xunit;

namespace PortalIUPA.Application.Tests;

public class DecidirSolicitudTests
{
    private readonly Mock<ISolicitudLicenciaRepository> _solicitudes = new();
    private readonly Mock<ITipoLicenciaRepository> _tipos = new();
    private readonly Mock<IAprobacionRepository> _aprobaciones = new();
    private readonly Mock<IEmpleadoRepository> _empleados = new();
    private readonly Mock<IRelacionACargoRepository> _relaciones = new();
    private readonly Mock<INotificacionRepository> _notificaciones = new();
    private readonly Mock<IEmailPort> _email = new();

    private readonly Empleado _solicitante;
    private readonly Empleado _responsable;
    private readonly TipoLicencia _tipo;
    private readonly SolicitudLicencia _solicitud;
    private readonly Guid _solicitudId;

    public DecidirSolicitudTests()
    {
        _solicitante = new Empleado(1, "Ana", "García", new Email("ana@iupa.edu.ar"));
        _responsable = new Empleado(2, "Luis", "Pérez", new Email("luis@iupa.edu.ar"));

        _tipo = new TipoLicencia("Médica");
        _tipo.AgregarNivel(AprobadorRequerido.ResponsableDirecto);
        _tipo.AgregarNivel(AprobadorRequerido.Rrhh);

        _solicitud = new SolicitudLicencia(_solicitante.Id, _tipo.Id,
            new RangoFechas(new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12)));
        _solicitudId = _solicitud.Id;

        // El responsable directo de Ana es Luis
        var relacion = new RelacionACargo(_responsable.Id, _solicitante.Id, true, new DateOnly(2026, 1, 1));
        _relaciones.Setup(r => r.GetVigenteDeEmpleadoAsync(_solicitante.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(relacion);

        _solicitudes.Setup(r => r.GetByIdAsync(_solicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_solicitud);
        _tipos.Setup(r => r.GetByIdAsync(_tipo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_tipo);
        _aprobaciones.Setup(r => r.GetBySolicitudAsync(_solicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Aprobacion>());
        _empleados.Setup(r => r.GetByIdAsync(_solicitante.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_solicitante);
        _empleados.Setup(r => r.GetByIdAsync(_responsable.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_responsable);
        _empleados.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _solicitante, _responsable });
        _empleados.Setup(r => r.GetConRolAsync(It.IsAny<Rol>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Empleado>());
    }

    private DecidirSolicitudCommandHandler CrearHandler()
    {
        var resolver = new AprobadorResolver(_tipos.Object, _aprobaciones.Object, _empleados.Object,
            _relaciones.Object);
        var notificador = new NotificadorLicencias(_notificaciones.Object, _email.Object, new Microsoft.Extensions.Logging.Abstractions.NullLogger<PortalIUPA.Application.Services.NotificadorLicencias>());
        return new DecidirSolicitudCommandHandler(_solicitudes.Object, _tipos.Object, _aprobaciones.Object,
            _empleados.Object, resolver, notificador);
    }

    [Fact]
    public async Task ResponsableDirectoPuedeAprobarElPrimerNivel()
    {
        var handler = CrearHandler();

        var resultado = await handler.Handle(
            new DecidirSolicitudCommand(_solicitudId, _responsable.Id, true, "OK"), CancellationToken.None);

        resultado.Estado.Should().Be(EstadoSolicitud.EnEspera);
        resultado.Aprobaciones.Should().ContainSingle(a =>
            a.AprobadorId == _responsable.Id && a.Resultado == ResultadoAprobacion.Aprobado);
        _aprobaciones.Verify(r => r.AddAsync(It.IsAny<Aprobacion>(), It.IsAny<CancellationToken>()), Times.Once);
        _solicitudes.Verify(r => r.UpdateAsync(_solicitud, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QuienNoEsAprobadorDelNivelNoPuedeActuar()
    {
        var intruso = new Empleado(3, "Pato", "López", new Email("pato@iupa.edu.ar"));
        _empleados.Setup(r => r.GetByIdAsync(intruso.Id, It.IsAny<CancellationToken>())).ReturnsAsync(intruso);
        var handler = CrearHandler();

        var act = async () => await handler.Handle(
            new DecidirSolicitudCommand(_solicitudId, intruso.Id, true, null), CancellationToken.None);

        await act.Should().ThrowAsync<AccesoDenegadoException>();
        _aprobaciones.Verify(r => r.AddAsync(It.IsAny<Aprobacion>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RechazoEnElPrimerNivel_CortaLaCadena_Desaprobada()
    {
        var handler = CrearHandler();

        var resultado = await handler.Handle(
            new DecidirSolicitudCommand(_solicitudId, _responsable.Id, false, "No corresponde"),
            CancellationToken.None);

        resultado.Estado.Should().Be(EstadoSolicitud.Desaprobada);
        resultado.Aprobaciones.Should().ContainSingle().Which.Resultado.Should().Be(ResultadoAprobacion.Rechazado);
    }

    [Fact]
    public async Task AprobacionFinal_ApruebaLaSolicitud()
    {
        // Nivel 1 ya aprobado
        var nivel1 = _tipo.NivelesOrdenados[0];
        _aprobaciones.Setup(r => r.GetBySolicitudAsync(_solicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Aprobacion>
            {
                new(_solicitudId, nivel1.Id, _responsable.Id, ResultadoAprobacion.Aprobado, null),
            });

        // El nivel 2 lo aprueba RRHH
        var rrhh = new Empleado(4, "Ramiro", "Ruiz", new Email("rrhh@iupa.edu.ar"));
        rrhh.AgregarRol(Rol.Rrhh);
        _empleados.Setup(r => r.GetByIdAsync(rrhh.Id, It.IsAny<CancellationToken>())).ReturnsAsync(rrhh);
        _empleados.Setup(r => r.GetConRolAsync(Rol.Rrhh, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { rrhh });
        var handler = CrearHandler();

        var resultado = await handler.Handle(
            new DecidirSolicitudCommand(_solicitudId, rrhh.Id, true, "Aprobado por RRHH"), CancellationToken.None);

        resultado.Estado.Should().Be(EstadoSolicitud.Aprobada);
        resultado.Aprobaciones.Should().HaveCount(2);
    }
}