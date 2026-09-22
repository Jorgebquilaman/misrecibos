using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PortalIUPA.Application.UseCases.Recibos;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Infrastructure.Pdf.Trazabilidad;

namespace PortalIUPA.Api.Controllers;

[Authorize]
[Route("api/recibos")]
public sealed class RecibosController : ApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPdfTrazabilidadService _trazabilidad;
    private readonly IOptions<TrazabilidadOptions> _trazabilidadOptions;

    public RecibosController(IMediator mediator, IPdfTrazabilidadService trazabilidad,
        IOptions<TrazabilidadOptions> trazabilidadOptions)
    {
        _mediator = mediator;
        _trazabilidad = trazabilidad;
        _trazabilidadOptions = trazabilidadOptions;
    }

    [HttpGet("disponibles")]
    public async Task<IActionResult> Disponibles() =>
        Ok(await _mediator.Send(new GetRecibosDisponiblesQuery(EmpleadoId)));

    [HttpGet("historial")]
    public async Task<IActionResult> Historial() =>
        Ok(await _mediator.Send(new GetHistorialDescargasQuery(EmpleadoId)));

    [HttpPost("{periodoId:guid}/descargar")]
    public async Task<IActionResult> Descargar(Guid periodoId)
    {
        var resultado = await _mediator.Send(
            new DescargarReciboCommand(EmpleadoId, periodoId, OrigenDescarga.Web, Ip));
        var pdf = resultado.Pdf;
        try
        {
            pdf = await _trazabilidad.AddTraceabilityToBytesAsync(
                pdfBytes: pdf,
                inputFileName: resultado.NombreArchivo,
                userId: resultado.Legajo.ToString(),
                position: TrazabilidadPosicion.FooterRight,
                encoding: TrazabilidadCodificacion.Morse,
                registroJsonPath: _trazabilidadOptions.Value.RutaRegistro);
        }
        catch (Exception ex)
        {
            // No bloquear la descarga si falla la trazabilidad; se loguea y se entrega el PDF sin marca
            Console.Error.WriteLine($"[Trazabilidad] fallo al marcar recibo {resultado.NombreArchivo}: {ex.Message}");
        }
        return File(pdf, "application/pdf", resultado.NombreArchivo);
    }

    [HttpPost("{periodoId:guid}/enviar-email")]
    public async Task<IActionResult> EnviarPorEmail(Guid periodoId)
    {
        await _mediator.Send(new EnviarReciboPorEmailCommand(EmpleadoId, periodoId));
        return NoContent();
    }
}