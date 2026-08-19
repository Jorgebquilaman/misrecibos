using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalIUPA.Application.UseCases.Recibos;
using PortalIUPA.Domain.Enums;

namespace PortalIUPA.Api.Controllers;

[Authorize]
[Route("api/recibos")]
public sealed class RecibosController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public RecibosController(IMediator mediator) => _mediator = mediator;

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
        return File(resultado.Pdf, "application/pdf", resultado.NombreArchivo);
    }

    [HttpPost("{periodoId:guid}/enviar-email")]
    public async Task<IActionResult> EnviarPorEmail(Guid periodoId)
    {
        await _mediator.Send(new EnviarReciboPorEmailCommand(EmpleadoId, periodoId));
        return NoContent();
    }
}