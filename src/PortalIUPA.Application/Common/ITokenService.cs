using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Application.Common;

/// <summary>Emite el JWT propio del portal a partir de un empleado autenticado (roles incluidos).</summary>
public interface ITokenService
{
    string GenerarToken(Empleado empleado);
}