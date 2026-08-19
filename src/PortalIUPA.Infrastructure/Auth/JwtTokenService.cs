using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PortalIUPA.Application.Common;
using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Infrastructure.Auth;

public sealed class JwtOptions
{
    public string Secret { get; set; } = "";
    public string Emisor { get; set; } = "PortalIUPA";
    public string Audiencia { get; set; } = "PortalIUPA.Frontend";
    public int MinutosExpiracion { get; set; } = 480;
}

/// <summary>Emite el JWT propio del portal (sub = empleado, email, nombre y roles).</summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options) => _options = options.Value;

    public string GenerarToken(Empleado empleado)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, empleado.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, empleado.Correo.Valor),
            new(JwtRegisteredClaimNames.Name, empleado.NombreCompleto),
            new("legajo", empleado.Legajo.ToString()),
            new("areaId", empleado.AreaId?.ToString() ?? ""),
        };
        claims.AddRange(empleado.Roles.Select(r => new Claim("rol", r.ToString())));

        var llave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var credenciales = new SigningCredentials(llave, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Emisor,
            audience: _options.Audiencia,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_options.MinutosExpiracion),
            signingCredentials: credenciales);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}