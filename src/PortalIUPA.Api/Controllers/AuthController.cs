using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalIUPA.Application.UseCases.Auth;

namespace PortalIUPA.Api.Controllers;

[Route("api/auth")]
public sealed class AuthController : ApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _config;
    private readonly IAuthenticationSchemeProvider _schemeProvider;

    public AuthController(IMediator mediator, IConfiguration config, IAuthenticationSchemeProvider schemeProvider)
    {
        _mediator = mediator;
        _config = config;
        _schemeProvider = schemeProvider;
    }

    /// <summary>Inicia el flujo OAuth con Google (redirige a accounts.google.com).</summary>
    [HttpGet("login")]
    public async Task<IActionResult> Login()
    {
        if (await _schemeProvider.GetSchemeAsync(GoogleDefaults.AuthenticationScheme) is null)
            return BadRequest(new { error = "El inicio con Google no está configurado en este entorno." });
        return Challenge(new AuthenticationProperties { RedirectUri = "/" }, GoogleDefaults.AuthenticationScheme);
    }

    /// <summary>Indica si el login con Google está disponible en este entorno.</summary>
    [HttpGet("google-disponible")]
    public async Task<IActionResult> GoogleDisponible() =>
        Ok(new { disponible = await _schemeProvider.GetSchemeAsync(GoogleDefaults.AuthenticationScheme) is not null });

    /// <summary>Login de desarrollo sin Google (solo con Auth:DevLoginEnabled=true).</summary>
    [HttpPost("dev-login")]
    [AllowAnonymous]
    public async Task<IActionResult> DevLogin([FromBody] DevLoginRequest request)
    {
        if (!_config.GetValue<bool>("Auth:DevLoginEnabled"))
            return BadRequest(new { error = "El login de desarrollo está deshabilitado." });

        var sesion = await _mediator.Send(new AutenticarEmpleadoCommand(request.Correo, Ip, UserAgent));
        return Ok(sesion);
    }

    /// <summary>Devuelve los datos del empleado autenticado a partir del JWT.</summary>
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me() => Ok(new
    {
        EmpleadoId,
        Correo = User.FindFirstValue(ClaimTypes.Email),
        Nombre = User.FindFirstValue("name") ?? User.FindFirstValue(ClaimTypes.Name),
        Legajo = User.FindFirstValue("legajo"),
        AreaId = User.FindFirstValue("areaId"),
        Roles = User.FindAll("rol").Select(c => c.Value)
    });

    [HttpPost("logout")]
    public IActionResult Logout() => NoContent();
}

public sealed record DevLoginRequest(string Correo);