using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalIUPA.Api.Auth;
using PortalIUPA.Application.UseCases.Reportes;
using System.Text.Json;

namespace PortalIUPA.Api.Controllers.Reportes;

/// <summary>
/// Dashboards visuales: los ven/ejecutan Rrhh y Administradores; solo Administradores crean/editan.
/// </summary>
[Authorize(Policy = "Rrhh")]
[Route("api/dashboards")]
public sealed class DashboardsController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public DashboardsController(IMediator mediator) => _mediator = mediator;

    public sealed record GuardarDashboardRequest(
        Guid? Id, string Nombre, string? Descripcion, string QuerySql, string? Conexion, DashboardDefinicionDto Definicion);
    public sealed record EjecutarDashboardRequest(Dictionary<string, object?>? Valores);
    public sealed record VistaPreviaDashboardRequest(string QuerySql, string? Conexion, DashboardDefinicionDto Definicion, Dictionary<string, object?>? Valores);

    [HttpGet]
    public async Task<IActionResult> Todos(CancellationToken ct) =>
        Ok(await _mediator.Send(new ListarDashboardsQuery(UsuarioCorreo(), Roles()), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> PorId(Guid id, CancellationToken ct) =>
        Ok(await _mediator.Send(new ObtenerDashboardQuery(id, UsuarioCorreo() ?? "", Roles()), ct));

    [HttpPost("{id:guid}/execute")]
    public async Task<IActionResult> Ejecutar(Guid id, [FromBody] EjecutarDashboardRequest? request, CancellationToken ct) =>
        Ok(await _mediator.Send(new EjecutarDashboardQuery(id, AValores(request?.Valores), UsuarioCorreo() ?? "", Roles()), ct));

    [Authorize(Policy = "Administrador")]
    [HttpPost]
    public async Task<IActionResult> Guardar([FromBody] GuardarDashboardRequest request, CancellationToken ct) =>
        Ok(new { id = await _mediator.Send(new GuardarDashboardCommand(
            null, request.Nombre, request.Descripcion, request.QuerySql, request.Conexion,
            request.Definicion, UsuarioCorreo() ?? ""), ct) });

    [Authorize(Policy = "Administrador")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] GuardarDashboardRequest request, CancellationToken ct) =>
        Ok(new { id = await _mediator.Send(new GuardarDashboardCommand(
            id, request.Nombre, request.Descripcion, request.QuerySql, request.Conexion,
            request.Definicion, UsuarioCorreo() ?? ""), ct) });

    [Authorize(Policy = "Administrador")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new EliminarDashboardCommand(id, UsuarioCorreo() ?? ""), ct);
        return Ok(new { ok = true });
    }

    /// <summary>Ejecuta el dashboard en borrador (sin guardar) para la vista previa del builder.</summary>
    [Authorize(Policy = "Administrador")]
    [HttpPost("preview")]
    public async Task<IActionResult> VistaPrevia([FromBody] VistaPreviaDashboardRequest request, CancellationToken ct) =>
        Ok(await _mediator.Send(new VistaPreviaDashboardQuery(
            request.QuerySql, request.Conexion, request.Definicion, AValores(request.Valores),
            UsuarioCorreo() ?? "", Roles()), ct));

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
