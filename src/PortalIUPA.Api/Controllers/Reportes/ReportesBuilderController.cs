using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalIUPA.Api.Auth;
using PortalIUPA.Application.UseCases.Reportes;
using System.Text.Json;

namespace PortalIUPA.Api.Controllers.Reportes;

/// <summary>
/// Reportes del builder: solo Administradores y Rrhh pueden ver/ejecutar.
/// La creación/edición queda limitada a Administradores.
/// </summary>
[Authorize(Policy = "Rrhh")]
[Route("api/reportes-builder")]
public sealed class ReportesBuilderController : ApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly IMotorReportes _motor;
    private readonly PortalIUPA.Infrastructure.Pdf.Trazabilidad.IPdfTrazabilidadService _trazabilidad;
    private readonly Microsoft.Extensions.Options.IOptions<PortalIUPA.Infrastructure.Pdf.Trazabilidad.TrazabilidadOptions> _opcionesTrazabilidad;

    public ReportesBuilderController(IMediator mediator, IMotorReportes motor,
        PortalIUPA.Infrastructure.Pdf.Trazabilidad.IPdfTrazabilidadService trazabilidad,
        Microsoft.Extensions.Options.IOptions<PortalIUPA.Infrastructure.Pdf.Trazabilidad.TrazabilidadOptions> opcionesTrazabilidad)
    {
        _mediator = mediator;
        _motor = motor;
        _trazabilidad = trazabilidad;
        _opcionesTrazabilidad = opcionesTrazabilidad;
    }

    public sealed record CrearReporteRequest(string Nombre, string? Descripcion, string QuerySql, string? Conexion, ReporteDefinicionDto Definicion);
    public sealed record EjecutarRequest(Dictionary<string, object?>? Valores);
    public sealed record EjecutarSubreporteRequest(Dictionary<string, object?> ValoresFila, Dictionary<string, object?>? Valores);
    public sealed record PermisosRequest(IReadOnlyList<PermisoDto> Permisos);
    public sealed record MetadataDto(IReadOnlyList<ColumnaMetadata> Columnas);

    /// <summary>Tablas/vistas de una conexión (asistente visual de consultas).</summary>
    [Authorize(Policy = "Administrador")]
    [HttpGet("conexiones/{nombre}/tablas")]
    public async Task<IActionResult> Tablas(string nombre, CancellationToken ct) =>
        Ok(await _mediator.Send(new TablasConexionQuery(nombre, UsuarioCorreo() ?? "", Roles()), ct));

    /// <summary>Columnas y relaciones FK de una tabla.</summary>
    [Authorize(Policy = "Administrador")]
    [HttpGet("conexiones/{nombre}/tablas/{esquema}/{tabla}")]
    public async Task<IActionResult> TablaDetalle(string nombre, string esquema, string tabla, CancellationToken ct) =>
        Ok(await _mediator.Send(new TablaDetalleQuery(nombre, esquema, tabla, UsuarioCorreo() ?? "", Roles()), ct));

    /// <summary>Conexiones Postgres disponibles para los reportes (nombres, sin secretos).</summary>
    [HttpGet("conexiones")]
    public IActionResult Conexiones() => Ok(new { conexiones = _motor.ConexionesDisponibles });

    /// <summary>Lista de reportes visibles para el usuario (por permiso, rol o creación).</summary>
    [HttpGet]
    public async Task<IActionResult> Todos(CancellationToken ct) =>
        Ok(await _mediator.Send(new ListarReportesQuery(UsuarioCorreo(), Roles()), ct));

    /// <summary>Definición completa del reporte (para editar o ver).</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> PorId(Guid id, CancellationToken ct) =>
        Ok(await _mediator.Send(new ObtenerReporteQuery(id, UsuarioCorreo() ?? "", Roles()), ct));

    /// <summary>Columnas disponibles de la consulta base (nombre + tipo).</summary>
    [HttpGet("{id:guid}/metadata")]
    public async Task<IActionResult> Metadata(Guid id, CancellationToken ct) =>
        Ok(new MetadataDto(await _mediator.Send(new MetadataReporteQuery(id, UsuarioCorreo() ?? "", Roles()), ct)));

    /// <summary>Ejecuta la consulta SQL en borrador y devuelve hasta 100 filas (para ver qué devuelve).</summary>
    [Authorize(Policy = "Administrador")]
    [HttpPost("preview-sql")]
    public async Task<IActionResult> PreviewSql([FromBody] PreviewSqlRequest request, CancellationToken ct) =>
        Ok(await _mediator.Send(new VistaPreviaSqlQuery(
            request.QuerySql, request.Conexion, UsuarioCorreo() ?? "", Roles()), ct));

    public sealed record PreviewSqlRequest(string QuerySql, string? Conexion);

    /// <summary>Columnas (nombre + tipo) de la consulta SQL en borrador, para el panel de campos.</summary>
    [Authorize(Policy = "Administrador")]
    [HttpPost("preview-metadata")]
    public async Task<IActionResult> PreviewMetadata([FromBody] PreviewSqlRequest request, CancellationToken ct) =>
        Ok(new MetadataDto(await _mediator.Send(new MetadataSqlQuery(
            request.QuerySql, request.Conexion, UsuarioCorreo() ?? "", Roles()), ct)));

    /// <summary>Crea o actualiza un reporte (solo Administradores).</summary>
    [Authorize(Policy = "Administrador")]
    [HttpPost]
    public async Task<IActionResult> Guardar([FromBody] CrearReporteRequest request, CancellationToken ct)
    {
        var id = await _mediator.Send(new GuardarReporteCommand(
            null, request.Nombre, request.Descripcion, request.QuerySql, request.Conexion,
            request.Definicion, UsuarioCorreo() ?? ""), ct);
        return Ok(new { id });
    }

    [Authorize(Policy = "Administrador")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] CrearReporteRequest request, CancellationToken ct)
    {
        var guardado = await _mediator.Send(new GuardarReporteCommand(
            id, request.Nombre, request.Descripcion, request.QuerySql, request.Conexion,
            request.Definicion, UsuarioCorreo() ?? ""), ct);
        return Ok(new { id = guardado });
    }

    [Authorize(Policy = "Administrador")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new EliminarReporteCommand(id, UsuarioCorreo() ?? ""), ct);
        return Ok(new { ok = true });
    }

    /// <summary>Ejecuta el reporte con los valores de filtros parametrizables.</summary>
    [HttpPost("{id:guid}/execute")]
    public async Task<IActionResult> Ejecutar(Guid id, [FromBody] EjecutarRequest? request, CancellationToken ct) =>
        Ok(await _mediator.Send(new EjecutarReporteQuery(id, AValores(request?.Valores), UsuarioCorreo() ?? "", Roles()), ct));

    /// <summary>Detalle (filas reales) de un grupo del reporte, para expandir la flecha.</summary>
    [HttpPost("{id:guid}/detalle")]
    public async Task<IActionResult> Detalle(Guid id, [FromBody] EjecutarRequest? request, CancellationToken ct) =>
        Ok(await _mediator.Send(new DetalleReporteQuery(
            id, AValores(request?.Valores) ?? new Dictionary<string, JsonElement>(), UsuarioCorreo() ?? "", Roles()), ct));

    /// <summary>Ejecuta un subreporte filtrado por los valores de una fila del reporte padre.</summary>
    [HttpPost("{id:guid}/subreportes/{indice:int}/execute")]
    public async Task<IActionResult> EjecutarSubreporte(
        Guid id, int indice, [FromBody] EjecutarSubreporteRequest request, CancellationToken ct) =>
        Ok(await _mediator.Send(new EjecutarSubreporteCommand(
            id, indice, AValores(request.ValoresFila)!, AValores(request.Valores), UsuarioCorreo() ?? "", Roles()), ct));

    /// <summary>Guarda el diseño de apariencia (canvas) del reporte.</summary>
    [Authorize(Policy = "Administrador")]
    [HttpPut("{id:guid}/diseno")]
    public async Task<IActionResult> Diseno(Guid id, [FromBody] DisenoRequest request, CancellationToken ct)
    {
        await _mediator.Send(new GuardarDisenoCommand(id, request.Diseno, UsuarioCorreo() ?? ""), ct);
        return Ok(new { ok = true });
    }

    /// <summary>
    /// Aplica la marca de trazabilidad (Morse footer-right) a un PDF generado
    /// (por ejemplo el export de un reporte hecho en el cliente) y registra la operación.
    /// </summary>
    [HttpPost("exportar/marcar")]
    public async Task<IActionResult> MarcarExport(IFormFile archivo, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { error = "Falta el archivo PDF a marcar." });

        await using var memoria = new MemoryStream();
        await archivo.CopyToAsync(memoria, ct);
        try
        {
            var marcado = await _trazabilidad.AddTraceabilityToBytesAsync(
                memoria.ToArray(), archivo.FileName, UsuarioCorreo() ?? "",
                registroJsonPath: _opcionesTrazabilidad.Value.RutaRegistro, ct: ct);
            return File(marcado, "application/pdf", archivo.FileName);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Trazabilidad] fallo al marcar export de reporte: {ex.Message}");
            // Fallback: devolvemos el PDF sin marca para no bloquear la descarga.
            return File(memoria.ToArray(), "application/pdf", archivo.FileName);
        }
    }

    public sealed record DisenoRequest(string Diseno);

    /// <summary>Reemplaza la lista de usuarios/roles autorizados a ver el reporte.</summary>
    [Authorize(Policy = "Administrador")]
    [HttpPut("{id:guid}/permisos")]
    public async Task<IActionResult> Permisos(Guid id, [FromBody] PermisosRequest request, CancellationToken ct)
    {
        await _mediator.Send(new AsignarPermisosCommand(id, request.Permisos, UsuarioCorreo() ?? ""), ct);
        return Ok(new { ok = true });
    }

    private static Dictionary<string, JsonElement>? AValores(Dictionary<string, object?>? valores)
    {
        if (valores is null) return null;
        var resultado = new Dictionary<string, JsonElement>();
        foreach (var (clave, valor) in valores)
        {
            if (valor is null) continue;
            resultado[clave] = JsonSerializer.SerializeToElement(valor);
        }
        return resultado;
    }

    private IReadOnlyList<string> Roles() => User.GetRoles();

    private string? UsuarioCorreo() =>
        User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ??
        User.FindFirst("email")?.Value;
}
