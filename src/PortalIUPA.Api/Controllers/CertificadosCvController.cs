using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalIUPA.Api.Auth;
using PortalIUPA.Application.UseCases.CertificadosCv;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Api.Controllers;

/// <summary>Certificados de cursos/carreras (CV) subidos por los empleados.</summary>
[Authorize]
[Route("api/cv-certificados")]
public sealed class CertificadosCvController : ApiControllerBase
{
    private const long MaxBytes = 10 * 1024 * 1024;

    private readonly IMediator _mediator;
    private readonly IGeneradorPdfCv _generadorCv;

    public CertificadosCvController(IMediator mediator, IGeneradorPdfCv generadorCv)
    {
        _mediator = mediator;
        _generadorCv = generadorCv;
    }

    /// <summary>Antecedentes académicos del empleado para su CV.</summary>
    [HttpGet("antecedentes")]
    public async Task<IActionResult> MisAntecedentes() =>
        Ok(await _mediator.Send(new ListarMisAntecedentesQuery(EmpleadoId)));

    [HttpPost("antecedentes")]
    [RequestSizeLimit(MaxBytes + 1024)]
    public async Task<IActionResult> CrearAntecedente([FromForm] string titulo, [FromForm] string institucion,
        [FromForm] string nivel, [FromForm] string? descripcion,
        [FromForm] DateOnly fechaDesde, [FromForm] DateOnly? fechaHasta, IFormFile archivo)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { error = "Debe adjuntar el título escaneado (PDF, JPG o PNG)." });
        await using var stream = archivo.OpenReadStream();
        var dto = await _mediator.Send(new SubirAntecedenteAcademicoCommand(
            EmpleadoId, titulo, institucion, nivel, descripcion, fechaDesde, fechaHasta,
            archivo.FileName, archivo.ContentType, stream));
        return CreatedAtAction(nameof(MisAntecedentes), new { id = dto.Id }, dto);
    }

    [HttpPut("antecedentes/{id:guid}")]
    public async Task<IActionResult> EditarAntecedente(Guid id, [FromBody] AntecedenteAcademicoRequest request)
    {
        await _mediator.Send(new EditarAntecedenteAcademicoCommand(EmpleadoId, id, request.Titulo,
            request.Institucion, request.Nivel, request.Descripcion, request.FechaDesde, request.FechaHasta));
        return NoContent();
    }

    [HttpDelete("antecedentes/{id:guid}")]
    public async Task<IActionResult> EliminarAntecedente(Guid id)
    {
        await _mediator.Send(new EliminarAntecedenteAcademicoCommand(EmpleadoId, id));
        return NoContent();
    }

    [HttpGet("antecedentes/{id:guid}/archivo")]
    public async Task<IActionResult> DescargarAntecedente(Guid id)
    {
        var (contenido, nombreArchivo, contentType) =
            await _mediator.Send(new DescargarAntecedenteAcademicoQuery(id, EmpleadoId, User.GetRoles()));
        return File(contenido, contentType, nombreArchivo);
    }

    /// <summary>Experiencias laborales del empleado para su CV.</summary>
    [HttpGet("experiencias")]
    public async Task<IActionResult> MisExperiencias() =>
        Ok(await _mediator.Send(new ListarMisExperienciasQuery(EmpleadoId)));

    [HttpPost("experiencias")]
    public async Task<IActionResult> CrearExperiencia([FromBody] ExperienciaCvRequest request) =>
        Ok(await _mediator.Send(new CrearExperienciaCvCommand(EmpleadoId, request.Puesto, request.Institucion,
            request.Descripcion, request.FechaDesde, request.FechaHasta)));

    [HttpPut("experiencias/{id:guid}")]
    public async Task<IActionResult> EditarExperiencia(Guid id, [FromBody] ExperienciaCvRequest request)
    {
        await _mediator.Send(new EditarExperienciaCvCommand(EmpleadoId, id, request.Puesto, request.Institucion,
            request.Descripcion, request.FechaDesde, request.FechaHasta));
        return NoContent();
    }

    [HttpDelete("experiencias/{id:guid}")]
    public async Task<IActionResult> EliminarExperiencia(Guid id)
    {
        await _mediator.Send(new EliminarExperienciaCvCommand(EmpleadoId, id));
        return NoContent();
    }

    /// <summary>Teléfono de contacto del CV.</summary>
    [HttpGet("telefono")]
    public async Task<IActionResult> ObtenerTelefono()
    {
        var cv = await _mediator.Send(new GenerarMiCvQuery(EmpleadoId));
        return Ok(new { telefono = cv.Datos.Empleado.Telefono });
    }

    [HttpPut("telefono")]
    public async Task<IActionResult> GuardarTelefono([FromBody] TelefonoCvRequest request)
    {
        var resultado = await _mediator.Send(new GuardarTelefonoCvCommand(EmpleadoId, request.Telefono));
        return Ok(new { telefono = resultado });
    }

    /// <summary>Observaciones libres que el empleado agrega a su CV.</summary>
    [HttpGet("observaciones")]
    public async Task<IActionResult> ObtenerObservaciones()
    {
        var cv = await _mediator.Send(new GenerarMiCvQuery(EmpleadoId));
        return Ok(new { observaciones = cv.Datos.Empleado.Observaciones });
    }

    [HttpPut("observaciones")]
    public async Task<IActionResult> GuardarObservaciones([FromBody] ObservacionesCvRequest request)
    {
        var resultado = await _mediator.Send(new GuardarObservacionesCvCommand(EmpleadoId, request.Observaciones));
        return Ok(new { observaciones = resultado });
    }

    /// <summary>Descarga el CV en PDF: datos personales + certificados con sus archivos adjuntos en un único archivo.</summary>
    [HttpGet("mi-cv")]
    public async Task<IActionResult> MiCv()
    {
        var cv = await _mediator.Send(new GenerarMiCvQuery(EmpleadoId));
        var pdf = await _generadorCv.GenerarAsync(cv.Datos, cv.Archivos);
        return File(pdf, "application/pdf", $"CV_{datosNombre(cv)}");
    }

    private static string datosNombre(CvCompletoDto cv) =>
        cv.Datos.Empleado.ApellidoYNombre.Replace(' ', '_') + "_" + DateTime.Today.ToString("yyyyMMdd") + ".pdf";

    [HttpGet("mios")]
    public async Task<IActionResult> Mios() =>
        Ok(await _mediator.Send(new GetMisCertificadosCvQuery(EmpleadoId)));

    [HttpPost]
    [RequestSizeLimit(MaxBytes + 1024)]
    public async Task<IActionResult> Subir([FromForm] string nombre, [FromForm] string institucion,
        [FromForm] string tipo, [FromForm] DateOnly fechaObtencion, IFormFile archivo)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { error = "Debe adjuntar el archivo del certificado." });

        await using var stream = archivo.OpenReadStream();
        var resultado = await _mediator.Send(new SubirCertificadoCvCommand(
            EmpleadoId, nombre, institucion, tipo, fechaObtencion,
            archivo.FileName, archivo.ContentType, stream));

        return CreatedAtAction(nameof(Mios), new { id = resultado.Id }, resultado);
    }

    [HttpGet("{id:guid}/archivo")]
    public async Task<IActionResult> Descargar(Guid id)
    {
        var (contenido, nombreArchivo, contentType) =
            await _mediator.Send(new DescargarCertificadoCvQuery(id, EmpleadoId, User.GetRoles()));
        return File(contenido, contentType, nombreArchivo);
    }

    /// <summary>Listado completo para RRHH/Administración.</summary>
    [Authorize(Policy = "Rrhh")]
    [HttpGet("admin")]
    public async Task<IActionResult> Admin([FromQuery] string? estado) =>
        Ok(await _mediator.Send(new ListarCertificadosCvQuery(estado)));

    /// <summary>Revisa un certificado: verificado u observado con comentario.</summary>
    [Authorize(Policy = "Rrhh")]
    [HttpPatch("{id:guid}/revisar")]
    public async Task<IActionResult> Revisar(Guid id, [FromBody] RevisarCertificadoCvRequest request)
    {
        var resultado = await _mediator.Send(new RevisarCertificadoCvCommand(id, request.Verificado, request.Comentario));
        return Ok(resultado);
    }

    [Authorize(Policy = "Rrhh")]
    [HttpPost("admin/aprobar-todos")]
    public async Task<IActionResult> AprobarTodos()
    {
        var count = await _mediator.Send(new AprobarTodosCertificadosCvCommand());
        return Ok(new { aprobados = count });
    }
}

public sealed record RevisarCertificadoCvRequest(bool Verificado, string? Comentario);

public sealed record ObservacionesCvRequest(string? Observaciones);

public sealed record TelefonoCvRequest(string? Telefono);

public sealed record ExperienciaCvRequest(
    string Puesto, string Institucion, string? Descripcion, DateOnly FechaDesde, DateOnly? FechaHasta);

public sealed record AntecedenteAcademicoRequest(
    string Titulo, string Institucion, string Nivel, string? Descripcion, DateOnly FechaDesde, DateOnly? FechaHasta);
