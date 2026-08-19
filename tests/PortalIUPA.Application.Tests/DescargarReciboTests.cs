using FluentAssertions;
using Moq;
using PortalIUPA.Application.Common;
using PortalIUPA.Application.UseCases.Recibos;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.Ports;
using PortalIUPA.Domain.ValueObjects;
using Xunit;

namespace PortalIUPA.Application.Tests;

public class DescargarReciboTests
{
    private readonly Mock<IEmpleadoRepository> _empleados = new();
    private readonly Mock<IPeriodoRepository> _periodos = new();
    private readonly Mock<IDescargaReciboRepository> _descargas = new();
    private readonly Mock<IJasperReportClient> _jasper = new();

    private readonly Empleado _empleado;
    private readonly Periodo _periodo;

    public DescargarReciboTests()
    {
        _empleado = new Empleado(1001, "Ana", "García", new Email("ana@iupa.edu.ar"));
        _periodo = new Periodo("2026-06", "Junio 2026", 163);
        _periodo.Activar();
    }

    private DescargarReciboCommandHandler CrearHandler() =>
        new(_empleados.Object, _periodos.Object, _descargas.Object, _jasper.Object);

    [Fact]
    public async Task Descarga_LlamaAJasperConLosParametrosDelLegacy_YRegistraLaAuditoria()
    {
        _empleados.Setup(r => r.GetByIdAsync(_empleado.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_empleado);
        _periodos.Setup(r => r.GetByIdAsync(_periodo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_periodo);
        _jasper.Setup(r => r.GetPdfAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });
        var handler = CrearHandler();

        var resultado = await handler.Handle(
            new DescargarReciboCommand(_empleado.Id, _periodo.Id, OrigenDescarga.Web, "10.0.0.1"),
            CancellationToken.None);

        resultado.Pdf.Should().NotBeEmpty();
        resultado.NombreArchivo.Should().Be("Recibo_2026-06_1001.pdf");
        _jasper.Verify(r => r.GetPdfAsync(
            "Mapuche/Reportes/Recibos_de_Sueldo_simple",
            It.Is<IReadOnlyDictionary<string, string>>(p =>
                p["nroliq"] == "163" && p["nroleg_f"] == "1001" && p["nroleg_i"] == "1001"),
            It.IsAny<CancellationToken>()), Times.Once);
        _descargas.Verify(r => r.AddAsync(
            It.Is<DescargaRecibo>(d => d.Origen == OrigenDescarga.Web && d.PeriodoId == _periodo.Id &&
                                       d.EmpleadoId == _empleado.Id && d.Ip == "10.0.0.1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Descarga_RechazaPeriodoInactivo()
    {
        var inactivo = new Periodo("2025-01");
        _empleados.Setup(r => r.GetByIdAsync(_empleado.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_empleado);
        _periodos.Setup(r => r.GetByIdAsync(inactivo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(inactivo);
        var handler = CrearHandler();

        var act = async () => await handler.Handle(
            new DescargarReciboCommand(_empleado.Id, inactivo.Id, OrigenDescarga.Web, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ReglaDeNegocioException>();
        _jasper.Verify(r => r.GetPdfAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Descarga_RechazaEmpleadoInexistente()
    {
        var handler = CrearHandler();

        var act = async () => await handler.Handle(
            new DescargarReciboCommand(Guid.NewGuid(), _periodo.Id, OrigenDescarga.Web, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<EntidadNoEncontradaException>();
    }
}