using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalIUPA.Application.UseCases.Licencias;

namespace PortalIUPA.Api.Controllers;

[Authorize]
[Route("api/licencias")]
public sealed class LicenciasController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public LicenciasController(IMediator mediator) => _mediator = mediator;

    [HttpGet("tipos")]
    public async Task<IActionResult> Tipos() =>
        Ok(await _mediator.Send(new GetTiposLicenciaQuery()));

    [HttpGet("consumo")]
    public async Task<IActionResult> Consumo([FromQuery] int anio, [FromQuery] int mes) =>
        Ok(await _mediator.Send(new GetConsumoLicenciasQuery(EmpleadoId, anio, mes)));

    [HttpGet("mis-solicitudes")]
    public async Task<IActionResult> MisSolicitudes() =>
        Ok(await _mediator.Send(new GetMisSolicitudesQuery(EmpleadoId)));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detalle(Guid id) =>
        Ok(await _mediator.Send(new GetSolicitudDetalleQuery(id, EmpleadoId)));

    [HttpPost]
    public async Task<IActionResult> Solicitar([FromBody] SolicitarLicenciaRequest request)
    {
        var resultado = await _mediator.Send(new SolicitarLicenciaCommand(EmpleadoId, request.TipoLicenciaId,
            request.FechaInicio, request.FechaFin, request.Asunto, request.Motivo, request.AdjuntoId));
        return CreatedAtAction(nameof(Detalle), new { id = resultado.SolicitudId }, resultado);
    }

    [HttpPost("{id:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid id)
    {
        await _mediator.Send(new CancelarSolicitudCommand(id, EmpleadoId));
        return NoContent();
    }

    [Authorize(Policy = "Responsable")]
    [HttpGet("pendientes")]
    public async Task<IActionResult> PendientesDeMiAprobacion() =>
        Ok(await _mediator.Send(new GetPendientesDeMiAprobacionQuery(EmpleadoId)));

    [Authorize(Policy = "Rrhh")]
    [HttpGet("pendientes-globales")]
    public async Task<IActionResult> PendientesGlobales() =>
        Ok(await _mediator.Send(new GetPendientesGlobalesQuery()));

    [Authorize(Policy = "Responsable")]
    [HttpPost("{id:guid}/decidir")]
    public async Task<IActionResult> Decidir(Guid id, [FromBody] DecidirSolicitudRequest request)
    {
        var resultado = await _mediator.Send(
            new DecidirSolicitudCommand(id, EmpleadoId, request.Aprobado, request.Comentario));
        return Ok(resultado);
    }
}

public sealed record SolicitarLicenciaRequest(Guid TipoLicenciaId, DateOnly FechaInicio, DateOnly FechaFin,
    string? Asunto, string? Motivo, Guid? AdjuntoId);

public sealed record DecidirSolicitudRequest(bool Aprobado, string? Comentario);