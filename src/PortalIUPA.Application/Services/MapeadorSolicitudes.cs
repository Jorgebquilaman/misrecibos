using PortalIUPA.Application.DTOs;
using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Application.Services;

/// <summary>Mapeo manual de SolicitudLicencia → DTO (con nombres de tipo, empleado y aprobadores precargados).</summary>
public static class MapeadorSolicitudes
{
    public static SolicitudLicenciaDto Mapear(SolicitudLicencia solicitud,
        IReadOnlyDictionary<Guid, TipoLicencia> tipos, IReadOnlyDictionary<Guid, Empleado> empleados,
        IEnumerable<Aprobacion> aprobaciones)
    {
        var tipo = tipos.GetValueOrDefault(solicitud.TipoLicenciaId);
        var empleado = empleados.GetValueOrDefault(solicitud.EmpleadoId);

        var aprobacionDtos = aprobaciones
            .Select(a =>
            {
                var nivel = tipo?.Niveles.FirstOrDefault(n => n.Id == a.NivelAprobacionId);
                var aprobador = empleados.GetValueOrDefault(a.AprobadorId);
                return new AprobacionDto(a.Id, a.NivelAprobacionId, nivel?.Orden ?? 0,
                    nivel?.RolRequerido.ToString() ?? "?", a.AprobadorId, aprobador?.NombreCompleto ?? "?",
                    a.Resultado, a.Comentario, a.FechaHora);
            })
            .OrderBy(a => a.NivelOrden)
            .ToList();

        return new SolicitudLicenciaDto(
            solicitud.Id,
            solicitud.EmpleadoId,
            empleado?.NombreCompleto ?? "?",
            solicitud.TipoLicenciaId,
            tipo?.Nombre ?? "?",
            solicitud.FechaInicio,
            solicitud.FechaFin,
            solicitud.Dias,
            solicitud.Asunto,
            solicitud.Motivo,
            solicitud.AdjuntoId,
            solicitud.Estado,
            solicitud.FechaSolicitud,
            aprobacionDtos);
    }
}