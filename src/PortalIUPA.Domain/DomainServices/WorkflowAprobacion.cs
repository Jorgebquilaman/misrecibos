using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Enums;

namespace PortalIUPA.Domain.DomainServices;

/// <summary>
/// Motor puro del workflow de aprobación multinivel.
/// Determina qué nivel debe actuar, el estado resultante de la solicitud y si la cadena está completa.
/// </summary>
public static class WorkflowAprobacion
{
    /// <summary>
    /// Devuelve el próximo nivel que debe actuar, o null si la cadena está completa o fue rechazada.
    /// Un nivel se considera resuelto por su última decisión (Aprobado avanza, Rechazado corta la cadena).
    /// </summary>
    public static NivelAprobacion? SiguienteNivelPendiente(TipoLicencia tipo, IEnumerable<Aprobacion> decisiones)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        var ultimaPorNivel = decisiones
            .GroupBy(d => d.NivelAprobacionId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.FechaHora).First());

        foreach (var nivel in tipo.NivelesOrdenados)
        {
            if (!ultimaPorNivel.TryGetValue(nivel.Id, out var ultima))
                return nivel;
            if (ultima.Resultado == ResultadoAprobacion.Rechazado)
                return null;
        }

        return null;
    }

    /// <summary>Estado que le corresponde a la solicitud según la cadena de niveles y las decisiones tomadas.</summary>
    public static EstadoSolicitud CalcularEstado(TipoLicencia tipo, IEnumerable<Aprobacion> decisiones)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        var decisionesLista = decisiones.ToList();
        if (decisionesLista.Any(d => d.Resultado == ResultadoAprobacion.Rechazado))
            return EstadoSolicitud.Desaprobada;

        return SiguienteNivelPendiente(tipo, decisionesLista) is null
            ? EstadoSolicitud.Aprobada
            : EstadoSolicitud.EnEspera;
    }

    /// <summary>Indica si la solicitud (según su cadena) aún espera decisiones.</summary>
    public static bool EstaPendiente(TipoLicencia tipo, IEnumerable<Aprobacion> decisiones) =>
        SiguienteNivelPendiente(tipo, decisiones) is not null;

    /// <summary>Indica si el empleado está habilitado para decidir sobre un nivel (por membresía de rol).</summary>
    public static bool TieneElRolRequerido(Empleado empleado, AprobadorRequerido requerido)
    {
        ArgumentNullException.ThrowIfNull(empleado);

        return requerido switch
        {
            AprobadorRequerido.Responsable => empleado.TieneRol(Rol.Responsable),
            AprobadorRequerido.Rrhh => empleado.TieneRol(Rol.Rrhh),
            AprobadorRequerido.Direccion => empleado.TieneRol(Rol.Direccion),
            AprobadorRequerido.Administrador => empleado.TieneRol(Rol.Administrador),
            AprobadorRequerido.ResponsableDirecto => false,
            _ => false,
        };
    }
}