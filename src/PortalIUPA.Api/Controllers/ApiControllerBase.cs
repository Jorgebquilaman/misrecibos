using Microsoft.AspNetCore.Mvc;
using PortalIUPA.Api.Auth;

namespace PortalIUPA.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Id del empleado autenticado (sub del JWT).</summary>
    protected Guid EmpleadoId => User.GetEmpleadoId();

    protected string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    protected string? UserAgent => HttpContext.Request.Headers.UserAgent.ToString();
}