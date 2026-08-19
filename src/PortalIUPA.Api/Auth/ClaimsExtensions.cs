using System.Security.Claims;

namespace PortalIUPA.Api.Auth;

public static class ClaimsExtensions
{
    public static Guid GetEmpleadoId(this ClaimsPrincipal usuario)
    {
        var sub = usuario.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? usuario.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("El token no contiene el identificador del empleado.");
        return Guid.Parse(sub);
    }

    public static IReadOnlyList<string> GetRoles(this ClaimsPrincipal usuario) =>
        usuario.FindAll("rol").Select(c => c.Value).ToList();
}