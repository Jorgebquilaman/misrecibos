using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalIUPA.Api.Auth;
using PortalIUPA.Application.UseCases.Fichadas;

namespace PortalIUPA.Api.Controllers;

[Authorize]
[Route("api/fichadas")]
public sealed class FichadasController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public FichadasController(IMediator mediator) => _mediator = mediator;

    [HttpGet("mias")]
    public async Task<IActionResult> Mias([FromQuery] int anio, [FromQuery] int mes) =>
        Ok(await _mediator.Send(new GetMisFichadasQuery(EmpleadoId, anio, mes)));

    [Authorize(Policy = "Responsable")]
    [HttpGet("asistencia")]
    public async Task<IActionResult> Asistencia([FromQuery] Guid? areaId, [FromQuery] DateOnly desde,
        [FromQuery] DateOnly hasta) =>
        Ok(await _mediator.Send(new GetAsistenciaPorAreaQuery(areaId, desde, hasta)));

    /// <summary>Carga manual de una marca de ingreso/egreso (HomeOffice para sí mismo; staff para cualquier empleado).</summary>
    [HttpPost("marcas-manuales")]
    public async Task<IActionResult> CrearMarcaManual([FromBody] CrearMarcaManualRequest request) =>
        Ok(await _mediator.Send(new CrearMarcaManualCommand(EmpleadoId, User.GetRoles(), request.EmpleadoId,
            request.FechaHora, request.Tipo)));

    /// <summary>Lista marcas manuales (propias; staff puede filtrar por empleado).</summary>
    [HttpGet("marcas-manuales")]
    public async Task<IActionResult> MarcasManuales([FromQuery] Guid? empleadoId, [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        return Ok(await _mediator.Send(new GetMarcasManualesQuery(EmpleadoId, User.GetRoles(), empleadoId,
            desde ?? new DateOnly(hoy.Year, hoy.Month, 1), hasta ?? hoy)));
    }

    /// <summary>Empleados autorizados a cargar marcas manuales (HomeOffice activos).</summary>
    [Authorize(Policy = "Responsable")]
    [HttpGet("marcas-manuales/empleados")]
    public async Task<IActionResult> EmpleadosHomeOffice() =>
        Ok(await _mediator.Send(new GetHomeOfficeEmpleadosQuery()));
}

public sealed record CrearMarcaManualRequest(Guid? EmpleadoId, DateTime FechaHora, string Tipo);