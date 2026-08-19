using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalIUPA.Application.Common;
using PortalIUPA.Application.UseCases.Admin;
using PortalIUPA.Domain.Enums;

namespace PortalIUPA.Api.Controllers;

[Authorize(Policy = "Rrhh")]
[Route("api/admin/empleados")]
public sealed class EmpleadosController : ApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly IExportadorEmpleados _exportador;

    public EmpleadosController(IMediator mediator, IExportadorEmpleados exportador)
    {
        _mediator = mediator;
        _exportador = exportador;
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] string? texto, [FromQuery] int pagina = 1,
        [FromQuery] int tamano = 20, [FromQuery] string? orden = "Apellido", [FromQuery] bool descendente = false) =>
        Ok(await _mediator.Send(new GetEmpleadosQuery(texto, pagina, tamano, orden, descendente)));

    [HttpGet("exportar")]
    public async Task<IActionResult> Exportar([FromQuery] string formato = "xlsx", [FromQuery] string? texto = null,
        [FromQuery] string? orden = "Apellido", [FromQuery] bool descendente = false)
    {
        var empleados = await _mediator.Send(new ExportarEmpleadosQuery(texto, orden, descendente));
        var archivo = await _exportador.GenerarAsync(empleados, formato);
        return File(archivo.Bytes, archivo.ContentType, archivo.NombreArchivo);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detalle(Guid id) =>
        Ok(await _mediator.Send(new GetEmpleadoDetalleQuery(id)));

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearEmpleadoRequest request)
    {
        var empleado = await _mediator.Send(new CreateEmpleadoCommand(request.Legajo, request.Nombre,
            request.Apellido, request.Dni, request.Cuil, request.Correo, request.AreaId, request.Roles));
        return CreatedAtAction(nameof(Detalle), new { id = empleado.Id }, empleado);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] ActualizarEmpleadoRequest request)
    {
        var empleado = await _mediator.Send(
            new UpdateEmpleadoCommand(id, request.Nombre, request.Apellido, request.Dni, request.Cuil,
                request.AreaId));
        return Ok(empleado);
    }

    [HttpPatch("{id:guid}/activo")]
    public async Task<IActionResult> SetActivo(Guid id, [FromBody] SetActivoRequest request)
    {
        await _mediator.Send(new SetActivoEmpleadoCommand(id, request.Activo));
        return NoContent();
    }

    [HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> SetRoles(Guid id, [FromBody] SetRolesRequest request)
    {
        await _mediator.Send(new SetRolesEmpleadoCommand(id, request.Roles));
        return NoContent();
    }
}

public sealed record CrearEmpleadoRequest(int Legajo, string Nombre, string Apellido, string? Dni, string? Cuil,
    string Correo, Guid? AreaId, IReadOnlyList<Rol> Roles);

public sealed record ActualizarEmpleadoRequest(string Nombre, string Apellido, string? Dni, string? Cuil,
    Guid? AreaId);

public sealed record SetActivoEmpleadoRequest(bool Activo);

public sealed record SetRolesRequest(IReadOnlyList<Rol> Roles);