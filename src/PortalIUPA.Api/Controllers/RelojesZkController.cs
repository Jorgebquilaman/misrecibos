using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalIUPA.Application.Common;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Ports;
using PortalIUPA.Infrastructure.External.ZK;

namespace PortalIUPA.Api.Controllers;

/// <summary>Relojes ZKTeco (protocolo ZK directo, ej. X628-C): alta, prueba de conexión,
/// descarga de marcas de asistencia y vaciado de memoria. Solo Administrador.</summary>
[Authorize(Policy = "Administrador")]
[Route("api/relojes-zk")]
public sealed class RelojesZkController : ApiControllerBase
{
    private readonly IRelojZkRepository _relojes;
    private readonly IRelojZkDescargaRepository _descargas;
    private readonly ServicioRelojesZk _servicio;

    public RelojesZkController(IRelojZkRepository relojes, IRelojZkDescargaRepository descargas,
        ServicioRelojesZk servicio)
    {
        _relojes = relojes;
        _descargas = descargas;
        _servicio = servicio;
    }

    public sealed record RelojZkDto(Guid Id, string Nombre, string Ip, int Puerto, int CommKey, string Modo, bool Activo,
        DateTime? UltimaDescarga, int UltimaCantidad);
    public sealed record RelojZkInfoDto(string? Nombre, string? Serial, int Usuarios, int Marcas, DateTime? UltimaMarca);
    public sealed record DescargaDto(int Leidas, int Nuevas, int Duplicadas, int Desconocidos, string? Mensaje,
        IReadOnlyList<MarcaLeidaDto> Marcas);
    public sealed record MarcaLeidaDto(string Legajo, DateTime FechaHora, bool EsSalida, bool EnRango,
        bool LegajoDesconocido, bool Nueva);
    public sealed record RelojZkDescargaDto(Guid Id, Guid RelojZkId, string RelojNombre, DateTime Fecha,
        int Leidas, int Nuevas, int Duplicadas, int LegajosDesconocidos, string Estado, string? Mensaje);

    public sealed record GuardarRelojZkRequest(string Nombre, string Ip, int Puerto, int CommKey, bool Activo = true, string Modo = "directo");
    public sealed record DescargarRequest(DateTime? Desde, DateTime? Hasta);

    [HttpGet]
    public async Task<IActionResult> Todos(CancellationToken ct)
    {
        var lista = await _relojes.GetAllAsync(ct);
        return Ok(lista.Select(r => new RelojZkDto(r.Id, r.Nombre, r.Ip, r.Puerto, r.CommKey, r.Modo, r.Activo,
            r.UltimaDescarga, r.UltimaCantidad)));
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] GuardarRelojZkRequest request, CancellationToken ct)
    {
        var reloj = new RelojZk(request.Nombre, request.Ip, request.Puerto, request.CommKey, request.Activo, request.Modo);
        await _relojes.AddAsync(reloj, ct);
        return Ok(new RelojZkDto(reloj.Id, reloj.Nombre, reloj.Ip, reloj.Puerto, reloj.CommKey, reloj.Modo, reloj.Activo,
            reloj.UltimaDescarga, reloj.UltimaCantidad));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Editar(Guid id, [FromBody] GuardarRelojZkRequest request, CancellationToken ct)
    {
        var reloj = await _relojes.GetByIdAsync(id, ct)
            ?? throw new EntidadNoEncontradaException("El reloj no existe.");
        reloj.Editar(request.Nombre, request.Ip, request.Puerto, request.CommKey, request.Activo, request.Modo);
        await _relojes.UpdateAsync(reloj, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct)
    {
        var reloj = await _relojes.GetByIdAsync(id, ct)
            ?? throw new EntidadNoEncontradaException("El reloj no existe.");
        await _relojes.DeleteAsync(reloj, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/probar")]
    public async Task<IActionResult> Probar(Guid id, CancellationToken ct)
    {
        var reloj = await _relojes.GetByIdAsync(id, ct)
            ?? throw new EntidadNoEncontradaException("El reloj no existe.");
        var info = await _servicio.ProbarConexionAsync(reloj, ct);
        return Ok(new RelojZkInfoDto(info.Nombre, info.Serial, info.Usuarios, info.Marcas, info.UltimaMarca));
    }

    [HttpPost("{id:guid}/descargar")]
    public async Task<IActionResult> Descargar(Guid id, [FromBody] DescargarRequest? request, CancellationToken ct)
    {
        var reloj = await _relojes.GetByIdAsync(id, ct)
            ?? throw new EntidadNoEncontradaException("El reloj no existe.");
        var correo = UsuarioCorreo();
        var resultado = await _servicio.DescargarAsync(reloj, request?.Desde, request?.Hasta, correo, ct);
        return Ok(new DescargaDto(resultado.Leidas, resultado.Nuevas, resultado.Duplicadas,
            resultado.Desconocidos, resultado.Mensaje,
            resultado.Marcas.Select(m => new MarcaLeidaDto(m.Legajo, m.FechaHora, m.EsSalida, m.EnRango,
                m.LegajoDesconocido, m.Nueva)).ToList()));
    }

    [HttpPost("{id:guid}/vaciar")]
    public async Task<IActionResult> Vaciar(Guid id, CancellationToken ct)
    {
        var reloj = await _relojes.GetByIdAsync(id, ct)
            ?? throw new EntidadNoEncontradaException("El reloj no existe.");
        await _servicio.VaciarAsync(reloj, ct);
        return NoContent();
    }

    [HttpGet("descargas")]
    public async Task<IActionResult> Descargas(CancellationToken ct)
    {
        var log = await _descargas.GetUltimasAsync(50, ct);
        var relojes = (await _relojes.GetAllAsync(ct)).ToDictionary(r => r.Id, r => r.Nombre);
        return Ok(log.Select(d => new RelojZkDescargaDto(d.Id, d.RelojZkId,
            relojes.TryGetValue(d.RelojZkId, out var nombre) ? nombre : "(reloj eliminado)",
            d.Fecha, d.Leidas, d.Nuevas, d.Duplicadas, d.LegajosDesconocidos, d.Estado, d.Mensaje)));
    }

    private string? UsuarioCorreo() =>
        User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
        ?? User.FindFirst("email")?.Value;
}
