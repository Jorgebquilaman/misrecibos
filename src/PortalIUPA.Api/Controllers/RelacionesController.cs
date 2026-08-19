using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalIUPA.Application.UseCases.Admin;

namespace PortalIUPA.Api.Controllers;

[Authorize(Policy = "Rrhh")]
[Route("api/admin/relaciones")]
public sealed class RelacionesController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public RelacionesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("a-cargo/{responsableId:guid}")]
    public async Task<IActionResult> ACargo(Guid responsableId) =>
        Ok(await _mediator.Send(new GetACargoQuery(responsableId)));

    [HttpGet("organigrama")]
    public async Task<IActionResult> Organigrama() =>
        Ok(await _mediator.Send(new GetOrganigramaQuery()));

    [HttpPost]
    public async Task<IActionResult> Asignar([FromBody] AsignarResponsableRequest request)
    {
        var relacion = await _mediator.Send(new AsignarResponsableCommand(request.ResponsableId,
            request.EmpleadoId, request.AutorizaMarcas));
        return Ok(relacion);
    }

    [HttpDelete("{relacionId:guid}")]
    public async Task<IActionResult> Quitar(Guid relacionId)
    {
        var relacion = await _mediator.Send(new QuitarResponsableCommand(relacionId));
        return Ok(relacion);
    }
}

public sealed record AsignarResponsableRequest(Guid ResponsableId, Guid EmpleadoId, bool AutorizaMarcas);