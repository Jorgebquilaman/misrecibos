using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalIUPA.Application.Common;
using PortalIUPA.Application.UseCases.Admin;

namespace PortalIUPA.Api.Controllers;

[Authorize(Policy = "Rrhh")]
[Route("api/admin/reportes")]
public sealed class ReportesController : ApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly IExportadorTabular _exportador;

    public ReportesController(IMediator mediator, IExportadorTabular exportador)
    {
        _mediator = mediator;
        _exportador = exportador;
    }

    [HttpGet("fichadas")]
    public async Task<IActionResult> Fichadas([FromQuery] string tipo, [FromQuery] int? legajo,
        [FromQuery] DateOnly desde, [FromQuery] DateOnly hasta, [FromQuery] string formato = "pdf")
    {
        var reporte = await _mediator.Send(new GenerarReporteFichadasQuery(tipo, legajo, desde, hasta));
        var archivo = await _exportador.GenerarAsync(reporte, formato);
        return File(archivo.Bytes, archivo.ContentType, archivo.NombreArchivo);
    }
}