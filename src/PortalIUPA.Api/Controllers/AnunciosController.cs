using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalIUPA.Application.UseCases.Anuncios;
using PortalIUPA.Domain.Enums;

namespace PortalIUPA.Api.Controllers;

[Authorize]
[Route("api/anuncios")]
public sealed class AnunciosController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public AnunciosController(IMediator mediator) => _mediator = mediator;

    [HttpGet("feed")]
    public async Task<IActionResult> Feed() =>
        Ok(await _mediator.Send(new GetFeedAnunciosQuery(EmpleadoId)));

    [HttpPost("{id:guid}/leido")]
    public async Task<IActionResult> MarcarLeido(Guid id)
    {
        await _mediator.Send(new MarcarAnuncioLeidoCommand(id, EmpleadoId));
        return NoContent();
    }

    [Authorize(Policy = "Rrhh")]
    [HttpGet]
    public async Task<IActionResult> Admin() =>
        Ok(await _mediator.Send(new GetAnunciosAdminQuery()));

    [Authorize(Policy = "Rrhh")]
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearAnuncioRequest request)
    {
        var anuncio = await _mediator.Send(new CreateAnuncioCommand(request.Titulo, request.Cuerpo, EmpleadoId,
            request.Prioridad, request.Tipo, request.FechaDesde, request.FechaHasta, request.Alcance,
            request.AreaId, request.Rol));
        return CreatedAtAction(nameof(Admin), new { id = anuncio.Id }, anuncio);
    }

    [Authorize(Policy = "Rrhh")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] ActualizarAnuncioRequest request)
    {
        var anuncio = await _mediator.Send(new UpdateAnuncioCommand(id, request.Titulo, request.Cuerpo,
            request.Prioridad, request.Tipo, request.FechaDesde, request.FechaHasta, request.Alcance,
            request.AreaId, request.Rol));
        return Ok(anuncio);
    }

    [Authorize(Policy = "Rrhh")]
    [HttpPatch("{id:guid}/activo")]
    public async Task<IActionResult> SetActivo(Guid id, [FromBody] SetActivoRequest request)
    {
        await _mediator.Send(new SetActivoAnuncioCommand(id, request.Activo));
        return NoContent();
    }
}

public sealed record CrearAnuncioRequest(string Titulo, string Cuerpo, PrioridadAnuncio Prioridad = PrioridadAnuncio.Normal,
    TipoAnuncio Tipo = TipoAnuncio.Informativo, DateOnly? FechaDesde = null, DateOnly? FechaHasta = null,
    AlcanceAnuncio Alcance = AlcanceAnuncio.Todos, Guid? AreaId = null, Rol? Rol = null);

public sealed record ActualizarAnuncioRequest(string Titulo, string Cuerpo, PrioridadAnuncio Prioridad,
    TipoAnuncio Tipo, DateOnly? FechaDesde, DateOnly? FechaHasta, AlcanceAnuncio Alcance, Guid? AreaId, Rol? Rol);

public sealed record SetActivoRequest(bool Activo);