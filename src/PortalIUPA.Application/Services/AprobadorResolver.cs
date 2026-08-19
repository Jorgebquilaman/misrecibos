using PortalIUPA.Application.Common;
using PortalIUPA.Domain.DomainServices;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Application.Services;

/// <summary>
/// Resuelve quién debe aprobar cada nivel de la cadena de una solicitud:
/// ResponsableDirecto → relación "a cargo" vigente del solicitante; los demás → membresía de rol.
/// </summary>
public interface IAprobadorResolver
{
    Task<NivelAprobacion?> NivelPendienteAsync(SolicitudLicencia solicitud, CancellationToken ct = default);
    Task<IReadOnlyList<Empleado>> ResolverAprobadoresAsync(SolicitudLicencia solicitud, NivelAprobacion nivel,
        CancellationToken ct = default);
    Task<bool> PuedeActuarAsync(SolicitudLicencia solicitud, Empleado usuario, CancellationToken ct = default);
}

public sealed class AprobadorResolver : IAprobadorResolver
{
    private readonly ITipoLicenciaRepository _tipos;
    private readonly IAprobacionRepository _aprobaciones;
    private readonly IEmpleadoRepository _empleados;
    private readonly IRelacionACargoRepository _relaciones;

    public AprobadorResolver(ITipoLicenciaRepository tipos, IAprobacionRepository aprobaciones,
        IEmpleadoRepository empleados, IRelacionACargoRepository relaciones)
    {
        _tipos = tipos;
        _aprobaciones = aprobaciones;
        _empleados = empleados;
        _relaciones = relaciones;
    }

    public async Task<NivelAprobacion?> NivelPendienteAsync(SolicitudLicencia solicitud, CancellationToken ct)
    {
        var tipo = await _tipos.GetByIdAsync(solicitud.TipoLicenciaId, ct)
            ?? throw new EntidadNoEncontradaException("El tipo de licencia no existe.");
        var decisiones = await _aprobaciones.GetBySolicitudAsync(solicitud.Id, ct);
        return WorkflowAprobacion.SiguienteNivelPendiente(tipo, decisiones);
    }

    public async Task<IReadOnlyList<Empleado>> ResolverAprobadoresAsync(SolicitudLicencia solicitud,
        NivelAprobacion nivel, CancellationToken ct)
    {
        if (nivel.RolRequerido == AprobadorRequerido.ResponsableDirecto)
        {
            var relacion = await _relaciones.GetVigenteDeEmpleadoAsync(solicitud.EmpleadoId, ct);
            if (relacion is null)
                return Array.Empty<Empleado>();

            var responsable = await _empleados.GetByIdAsync(relacion.ResponsableId, ct);
            return responsable is { Activo: true } ? new[] { responsable } : Array.Empty<Empleado>();
        }

        var rol = nivel.RolRequerido switch
        {
            AprobadorRequerido.Responsable => Rol.Responsable,
            AprobadorRequerido.Rrhh => Rol.Rrhh,
            AprobadorRequerido.Direccion => Rol.Direccion,
            AprobadorRequerido.Administrador => Rol.Administrador,
            _ => Rol.Empleado,
        };

        return (await _empleados.GetConRolAsync(rol, ct)).Where(e => e.Activo).ToList();
    }

    public async Task<bool> PuedeActuarAsync(SolicitudLicencia solicitud, Empleado usuario, CancellationToken ct)
    {
        var nivel = await NivelPendienteAsync(solicitud, ct);
        if (nivel is null)
            return false;

        var aprobadores = await ResolverAprobadoresAsync(solicitud, nivel, ct);
        return aprobadores.Any(a => a.Id == usuario.Id);
    }
}