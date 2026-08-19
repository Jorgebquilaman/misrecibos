using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalIUPA.Application.UseCases.Admin;

namespace PortalIUPA.Api.Controllers;

[Authorize(Policy = "Rrhh")]
[Route("api/admin/areas")]
public sealed class AreasController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public AreasController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> Listar() =>
        Ok(await _mediator.Send(new GetAreasQuery()));

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearAreaRequest request)
    {
        var area = await _mediator.Send(new CreateAreaCommand(request.Nombre, request.Codigo, request.AreaPadreId));
        return Ok(area);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] ActualizarAreaRequest request)
    {
        var area = await _mediator.Send(new UpdateAreaCommand(id, request.Nombre, request.Codigo, request.AreaPadreId, request.Activa));
        return Ok(area);
    }
}

public sealed record CrearAreaRequest(string Nombre, string Codigo, Guid? AreaPadreId);

public sealed record ActualizarAreaRequest(string Nombre, string Codigo, Guid? AreaPadreId, bool? Activa);