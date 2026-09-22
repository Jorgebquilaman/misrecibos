using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalIUPA.Application.Common;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Api.Controllers;

/// <summary>Edificios con coordenadas para la detección de ubicación en marcas manuales.
/// Lectura para cualquier usuario autenticado; alta/edición/baja solo Administrador.</summary>
[Authorize]
[Route("api/edificios")]
public sealed class EdificiosController : ApiControllerBase
{
    private readonly IEdificioRepository _edificios;

    public EdificiosController(IEdificioRepository edificios) => _edificios = edificios;

    public sealed record EdificioDto(Guid Id, string Nombre, double Latitud, double Longitud, int RadioMetros, bool Activo);

    [HttpGet]
    public async Task<IActionResult> Todos(CancellationToken ct)
    {
        var lista = await _edificios.GetAllAsync(ct);
        return Ok(lista.Select(e => new EdificioDto(e.Id, e.Nombre, e.Latitud, e.Longitud, e.RadioMetros, e.Activo)));
    }

    [Authorize(Policy = "Administrador")]
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearEdificioRequest request, CancellationToken ct)
    {
        var edificio = new Edificio(request.Nombre, request.Latitud, request.Longitud, request.RadioMetros <= 0 ? 100 : request.RadioMetros);
        await _edificios.AddAsync(edificio, ct);
        return Ok(new EdificioDto(edificio.Id, edificio.Nombre, edificio.Latitud, edificio.Longitud, edificio.RadioMetros, edificio.Activo));
    }

    [Authorize(Policy = "Administrador")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Editar(Guid id, [FromBody] CrearEdificioRequest request, CancellationToken ct)
    {
        var edificio = await _edificios.GetByIdAsync(id, ct)
            ?? throw new EntidadNoEncontradaException("El edificio no existe.");
        edificio.Editar(request.Nombre, request.Latitud, request.Longitud, request.RadioMetros <= 0 ? 100 : request.RadioMetros, request.Activo);
        await _edificios.UpdateAsync(edificio, ct);
        return NoContent();
    }

    [Authorize(Policy = "Administrador")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
    {
        var edificio = await _edificios.GetByIdAsync(id, ct)
            ?? throw new EntidadNoEncontradaException("El edificio no existe.");
        await _edificios.DeleteAsync(edificio, ct);
        return NoContent();
    }
}

public sealed record CrearEdificioRequest(string Nombre, double Latitud, double Longitud, int RadioMetros, bool Activo = true);
