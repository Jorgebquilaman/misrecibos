using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalIUPA.Application.UseCases.Admin;

namespace PortalIUPA.Api.Controllers;

[Authorize(Policy = "Rrhh")]
[Route("api/admin/periodos")]
public sealed class PeriodosController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public PeriodosController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> Listar() =>
        Ok(await _mediator.Send(new GetPeriodosQuery()));

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearPeriodoRequest request)
    {
        var periodo = await _mediator.Send(new CreatePeriodoCommand(request.Codigo, request.Descripcion, request.NroLiq));
        return Ok(periodo);
    }

    [HttpPost("sincronizar")]
    public async Task<IActionResult> Sincronizar() =>
        Ok(await _mediator.Send(new SincronizarPeriodosCommand()));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] ActualizarPeriodoRequest request)
    {
        var periodo = await _mediator.Send(new UpdatePeriodoCommand(id, request.Descripcion, request.Activo, request.NroLiq));
        return Ok(periodo);
    }
}

public sealed record CrearPeriodoRequest(string Codigo, string? Descripcion, int? NroLiq);

public sealed record ActualizarPeriodoRequest(string? Descripcion, bool Activo, int? NroLiq);