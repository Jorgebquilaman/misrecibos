using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.ValueObjects;

namespace PortalIUPA.Domain.Ports;

public interface ISolicitudLicenciaRepository
{
    Task AddAsync(SolicitudLicencia solicitud, CancellationToken ct = default);
    Task<SolicitudLicencia?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SolicitudLicencia>> GetByEmpleadoAsync(Guid empleadoId, CancellationToken ct = default);
    Task<IReadOnlyList<SolicitudLicencia>> GetEnEsperaAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SolicitudLicencia>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Solicitudes del empleado (mismo tipo) que solapan el rango y no están resueltas ni canceladas.</summary>
    Task<IReadOnlyList<SolicitudLicencia>> GetSolapadasAsync(Guid empleadoId, Guid tipoLicenciaId, RangoFechas rango,
        CancellationToken ct = default);

    /// <summary>Días consumidos (aprobadas + en espera) del empleado para el tipo, dentro del mes y del año.</summary>
    Task<(int DiasMes, int DiasAnio)> GetConsumoAsync(Guid empleadoId, Guid tipoLicenciaId, int anio, int mes,
        CancellationToken ct = default);

    Task UpdateAsync(SolicitudLicencia solicitud, CancellationToken ct = default);
}