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

public class SolicitarLicenciaTests
{
    private static readonly Guid EmpleadoId = Guid.NewGuid();
    private readonly Mock<ITipoLicenciaRepository> _tipos = new();
    private readonly Mock<ISolicitudLicenciaRepository> _solicitudes = new();
    private readonly Mock<IEmpleadoRepository> _empleados = new();
    private readonly Mock<IAdjuntoRepository> _adjuntos = new();
    private readonly Mock<INotificacionRepository> _notificaciones = new();
    private readonly Mock<IEmailPort> _email = new();
    private readonly Mock<IRelacionACargoRepository> _relaciones = new();
    private readonly Mock<IAprobacionRepository> _aprobaciones = new();

    private SolicitarLicenciaCommandHandler CrearHandler()
    {
        var resolver = new AprobadorResolver(_tipos.Object, _aprobaciones.Object, _empleados.Object,
            _relaciones.Object);
        var notificador = new NotificadorLicencias(_notificaciones.Object, _email.Object, new Microsoft.Extensions.Logging.Abstractions.NullLogger<PortalIUPA.Application.Services.NotificadorLicencias>());
        return new SolicitarLicenciaCommandHandler(_tipos.Object, _solicitudes.Object, _empleados.Object,
            _adjuntos.Object, resolver, notificador);
    }

    private static TipoLicencia TipoParticular() =>
        new("Particular", limiteMensual: 5, limiteAnual: 20);

    private static Empleado Empleado() =>
        new(1, "Ana", "García", new Email("ana@iupa.edu.ar"));

    private SolicitarLicenciaCommand Comando(DateOnly inicio, DateOnly fin, Guid? adjuntoId = null) =>
        new(EmpleadoId, Guid.NewGuid(), inicio, fin, "Asunto", "Motivo", adjuntoId);

    [Fact]
    public async Task Solicita_YValidaLimitesYCreaLaSolicitud()
    {
        var tipo = TipoParticular();
        _tipos.Setup(r => r.GetByIdAsync(tipo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tipo);
        _empleados.Setup(r => r.GetByIdAsync(EmpleadoId, It.IsAny<CancellationToken>())).ReturnsAsync(Empleado());
        _solicitudes.Setup(r => r.GetConsumoAsync(EmpleadoId, tipo.Id, 2026, 8, It.IsAny<CancellationToken>()))
            .ReturnsAsync((2, 5));
        _solicitudes.Setup(r => r.GetSolapadasAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<RangoFechas>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SolicitudLicencia>());
        _aprobaciones.Setup(r => r.GetBySolicitudAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Aprobacion>());
        _relaciones.Setup(r => r.GetVigenteDeEmpleadoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RelacionACargo?)null);
        _empleados.Setup(r => r.GetConRolAsync(It.IsAny<Rol>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Empleado>());
        var handler = CrearHandler();

        var resultado = await handler.Handle(
            new SolicitarLicenciaCommand(EmpleadoId, tipo.Id, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12),
                "Asunto", "Motivo", null), CancellationToken.None);

        resultado.Estado.Should().Be(EstadoSolicitud.Aprobada);
        _solicitudes.Verify(r => r.AddAsync(It.IsAny<SolicitudLicencia>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RechazaCuandoSuperaElLimiteMensual()
    {
        var tipo = TipoParticular();
        _tipos.Setup(r => r.GetByIdAsync(tipo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tipo);
        _empleados.Setup(r => r.GetByIdAsync(EmpleadoId, It.IsAny<CancellationToken>())).ReturnsAsync(Empleado());
        _solicitudes.Setup(r => r.GetConsumoAsync(EmpleadoId, tipo.Id, 2026, 8, It.IsAny<CancellationToken>()))
            .ReturnsAsync((4, 5));
        var handler = CrearHandler();

        var act = async () => await handler.Handle(
            new SolicitarLicenciaCommand(EmpleadoId, tipo.Id, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12),
                null, null, null), CancellationToken.None);

        await act.Should().ThrowAsync<ReglaDeNegocioException>()
            .WithMessage("*mensual*");
        _solicitudes.Verify(r => r.AddAsync(It.IsAny<SolicitudLicencia>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RechazaCuandoHaySolicitudesSolapadas()
    {
        var tipo = TipoParticular();
        _tipos.Setup(r => r.GetByIdAsync(tipo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tipo);
        _empleados.Setup(r => r.GetByIdAsync(EmpleadoId, It.IsAny<CancellationToken>())).ReturnsAsync(Empleado());
        _solicitudes.Setup(r => r.GetConsumoAsync(EmpleadoId, tipo.Id, 2026, 8, It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, 0));
        _solicitudes.Setup(r => r.GetSolapadasAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<RangoFechas>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SolicitudLicencia>
            {
                new(EmpleadoId, tipo.Id, new RangoFechas(new DateOnly(2026, 8, 11), new DateOnly(2026, 8, 13))),
            });
        var handler = CrearHandler();

        var act = async () => await handler.Handle(
            new SolicitarLicenciaCommand(EmpleadoId, tipo.Id, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12),
                null, null, null), CancellationToken.None);

        await act.Should().ThrowAsync<ReglaDeNegocioException>()
            .WithMessage("*superpone*");
    }

    [Fact]
    public async Task RechazaCuandoElTipoRequiereAdjuntoYNohay()
    {
        var tipo = new TipoLicencia("Médica", requiereAdjunto: true);
        _tipos.Setup(r => r.GetByIdAsync(tipo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tipo);
        _empleados.Setup(r => r.GetByIdAsync(EmpleadoId, It.IsAny<CancellationToken>())).ReturnsAsync(Empleado());
        _solicitudes.Setup(r => r.GetConsumoAsync(EmpleadoId, tipo.Id, 2026, 8, It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, 0));
        var handler = CrearHandler();

        var act = async () => await handler.Handle(
            new SolicitarLicenciaCommand(EmpleadoId, tipo.Id, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12),
                null, null, null), CancellationToken.None);

        await act.Should().ThrowAsync<ReglaDeNegocioException>()
            .WithMessage("*requiere adjuntar*");
    }

    [Fact]
    public async Task RechazaTipoInactivo()
    {
        var tipo = TipoParticular();
        tipo.Desactivar();
        _tipos.Setup(r => r.GetByIdAsync(tipo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tipo);
        var handler = CrearHandler();

        var act = async () => await handler.Handle(
            new SolicitarLicenciaCommand(EmpleadoId, tipo.Id, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12),
                null, null, null), CancellationToken.None);

        await act.Should().ThrowAsync<ReglaDeNegocioException>()
            .WithMessage("*no está activo*");
    }
}