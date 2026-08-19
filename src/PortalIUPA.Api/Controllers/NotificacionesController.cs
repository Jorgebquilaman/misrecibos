using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalIUPA.Application.UseCases.Notificaciones;

namespace PortalIUPA.Api.Controllers;

[Authorize]
[Route("api/notificaciones")]
public sealed class NotificacionesController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public NotificacionesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> Mias() =>
        Ok(await _mediator.Send(new GetMisNotificacionesQuery(EmpleadoId)));

    [HttpGet("no-leidas-count")]
    public async Task<IActionResult> NoLeidasCount() =>
        Ok(await _mediator.Send(new GetNotificacionesNoLeidasCountQuery(EmpleadoId)));

    [HttpPost("{id:guid}/leida")]
    public async Task<IActionResult> MarcarLeida(Guid id)
    {
        await _mediator.Send(new MarcarNotificacionLeidaCommand(id, EmpleadoId));
        return NoContent();
    }
}