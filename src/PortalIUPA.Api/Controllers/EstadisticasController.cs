using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalIUPA.Application.UseCases.Estadisticas;

namespace PortalIUPA.Api.Controllers;

[Authorize(Policy = "Rrhh")]
[Route("api/estadisticas")]
public sealed class EstadisticasController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public EstadisticasController(IMediator mediator) => _mediator = mediator;

    [HttpGet("accesos")]
    public async Task<IActionResult> Accesos([FromQuery] DateOnly desde, [FromQuery] DateOnly hasta) =>
        Ok(await _mediator.Send(new GetEstadisticasAccesosQuery(desde, hasta)));

    [HttpGet("accesos/log")]
    public async Task<IActionResult> LogAccesos([FromQuery] DateOnly desde, [FromQuery] DateOnly hasta) =>
        Ok(await _mediator.Send(new GetAccesosQuery(desde, hasta)));
}