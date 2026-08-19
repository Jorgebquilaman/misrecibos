using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalIUPA.Application.UseCases.Certificados;
using PortalIUPA.Domain.Enums;

namespace PortalIUPA.Api.Controllers;

[Authorize]
[Route("api/certificados")]
public sealed class CertificadosController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public CertificadosController(IMediator mediator) => _mediator = mediator;

    [HttpGet("mios")]
    public async Task<IActionResult> Mios() =>
        Ok(await _mediator.Send(new GetMisCertificadosQuery(EmpleadoId)));

    [HttpPost]
    public async Task<IActionResult> Solicitar([FromBody] SolicitarCertificadoRequest request)
    {
        var certificado = await _mediator.Send(new SolicitarCertificadoCommand(EmpleadoId, request.Tipo,
            request.Desde, request.Hasta, request.Destino));
        return CreatedAtAction(nameof(Mios), new { id = certificado.Id }, certificado);
    }

    [HttpPost("{id:guid}/descargar")]
    public async Task<IActionResult> Descargar(Guid id)
    {
        var resultado = await _mediator.Send(new DescargarCertificadoCommand(id, EmpleadoId));
        return File(resultado.Pdf, "application/pdf", resultado.NombreArchivo);
    }
}

public sealed record SolicitarCertificadoRequest(TipoCertificado Tipo, DateOnly Desde, DateOnly Hasta,
    string? Destino);