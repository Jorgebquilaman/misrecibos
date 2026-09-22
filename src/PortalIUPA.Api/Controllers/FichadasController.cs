using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalIUPA.Api.Auth;
using PortalIUPA.Application.Common;
using PortalIUPA.Application.UseCases.Fichadas;

namespace PortalIUPA.Api.Controllers;

[Authorize]
[Route("api/fichadas")]
public sealed class FichadasController : ApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly IExportadorTabular _exportador;

    public FichadasController(IMediator mediator, IExportadorTabular exportador)
    {
        _mediator = mediator;
        _exportador = exportador;
    }

    [HttpGet("mias")]
    public async Task<IActionResult> Mias([FromQuery] int anio, [FromQuery] int mes) =>
        Ok(await _mediator.Send(new GetMisFichadasQuery(EmpleadoId, anio, mes)));

    /// <summary>Exporta las fichadas propias del mes en PDF o Excel.</summary>
    [HttpGet("mias/exportar")]
    public async Task<IActionResult> ExportarMias([FromQuery] int anio, [FromQuery] int mes,
        [FromQuery] string formato = "pdf")
    {
        var reporte = await _mediator.Send(new ExportarMisFichadasQuery(EmpleadoId, anio, mes));
        var archivo = await _exportador.GenerarAsync(reporte, formato);
        return File(archivo.Bytes, archivo.ContentType, archivo.NombreArchivo);
    }

    /// <summary>Exporta el listado crudo de todas las marcas propias del mes (reloj + manuales + descargadas).</summary>
    [HttpGet("mias/marcas/exportar")]
    public async Task<IActionResult> ExportarMarcasMes([FromQuery] int anio, [FromQuery] int mes,
        [FromQuery] string formato = "xlsx")
    {
        var reporte = await _mediator.Send(new ExportarMarcasMesQuery(EmpleadoId, anio, mes));
        var archivo = await _exportador.GenerarAsync(reporte, formato);
        return File(archivo.Bytes, archivo.ContentType, archivo.NombreArchivo);
    }

    [Authorize(Policy = "Responsable")]
    [HttpGet("asistencia")]
    public async Task<IActionResult> Asistencia([FromQuery] Guid? areaId, [FromQuery] DateOnly desde,
        [FromQuery] DateOnly hasta) =>
        Ok(await _mediator.Send(new GetAsistenciaPorAreaQuery(areaId, desde, hasta)));

    /// <summary>Carga manual de una marca de ingreso/egreso (HomeOffice para sí mismo; staff para cualquier empleado).</summary>
    [HttpPost("marcas-manuales")]
    public async Task<IActionResult> CrearMarcaManual([FromBody] CrearMarcaManualRequest request) =>
        Ok(await _mediator.Send(new CrearMarcaManualCommand(EmpleadoId, User.GetRoles(), request.EmpleadoId,
            request.FechaHora, request.Tipo, request.Latitud, request.Longitud)));

    /// <summary>Edita una marca manual propia o de un empleado si es staff. Replica el cambio en el reloj (MSSQL).</summary>
    [HttpPut("marcas-manuales/{id:guid}")]
    public async Task<IActionResult> EditarMarcaManual(Guid id, [FromBody] EditarMarcaManualRequest request) =>
        Ok(await _mediator.Send(new EditarMarcaManualCommand(EmpleadoId, User.GetRoles(), id,
            request.FechaHora, request.Tipo)));

    /// <summary>Elimina una marca manual propia o de un empleado si es staff. Replica la eliminación en el reloj (MSSQL).</summary>
    [HttpDelete("marcas-manuales/{id:guid}")]
    public async Task<IActionResult> EliminarMarcaManual(Guid id)
    {
        await _mediator.Send(new EliminarMarcaManualCommand(EmpleadoId, User.GetRoles(), id));
        return NoContent();
    }

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

public sealed record CrearMarcaManualRequest(Guid? EmpleadoId, DateTime FechaHora, string Tipo,
    double? Latitud = null, double? Longitud = null);

public sealed record EditarMarcaManualRequest(DateTime FechaHora, string Tipo);