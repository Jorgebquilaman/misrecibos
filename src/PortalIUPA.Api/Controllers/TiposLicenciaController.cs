using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalIUPA.Application.UseCases.Admin;
using PortalIUPA.Domain.Enums;

namespace PortalIUPA.Api.Controllers;

[Authorize(Policy = "Rrhh")]
[Route("api/admin/tipos-licencia")]
public sealed class TiposLicenciaController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public TiposLicenciaController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> Listar() =>
        Ok(await _mediator.Send(new GetTiposLicenciaAdminQuery()));

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearTipoLicenciaRequest request)
    {
        var tipo = await _mediator.Send(new CreateTipoLicenciaCommand(request.Nombre, request.LimiteMensual,
            request.LimiteAnual, request.Descripcion, request.RequiereAdjunto, request.Niveles));
        return Ok(tipo);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] ActualizarTipoLicenciaRequest request)
    {
        var tipo = await _mediator.Send(new UpdateTipoLicenciaCommand(id, request.Nombre, request.LimiteMensual,
            request.LimiteAnual, request.Descripcion, request.RequiereAdjunto));
        return Ok(tipo);
    }

    [HttpPatch("{id:guid}/activo")]
    public async Task<IActionResult> SetActivo(Guid id, [FromBody] SetActivoRequest request)
    {
        await _mediator.Send(new SetActivoTipoLicenciaCommand(id, request.Activo));
        return NoContent();
    }

    [HttpPost("{id:guid}/niveles")]
    public async Task<IActionResult> AgregarNivel(Guid id, [FromBody] AgregarNivelRequest request)
    {
        var tipo = await _mediator.Send(new AgregarNivelAprobacionCommand(id, request.RolRequerido));
        return Ok(tipo);
    }

    [HttpDelete("niveles/{nivelId:guid}")]
    public async Task<IActionResult> QuitarNivel(Guid nivelId)
    {
        var tipo = await _mediator.Send(new QuitarNivelAprobacionCommand(nivelId));
        return Ok(tipo);
    }

    [HttpPut("{id:guid}/niveles/orden")]
    public async Task<IActionResult> Reordenar(Guid id, [FromBody] ReordenarNivelesRequest request)
    {
        var tipo = await _mediator.Send(new ReordenarNivelesCommand(id, request.NivelIds));
        return Ok(tipo);
    }
}

public sealed record CrearTipoLicenciaRequest(string Nombre, int? LimiteMensual, int? LimiteAnual,
    string? Descripcion, bool RequiereAdjunto, IReadOnlyList<AprobadorRequerido> Niveles);

public sealed record ActualizarTipoLicenciaRequest(string Nombre, int? LimiteMensual, int? LimiteAnual,
    string? Descripcion, bool RequiereAdjunto);

public sealed record SetActivoTipoLicenciaRequest(bool Activo);

public sealed record AgregarNivelRequest(AprobadorRequerido RolRequerido);

public sealed record ReordenarNivelesRequest(IReadOnlyList<Guid> NivelIds);