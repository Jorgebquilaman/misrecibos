using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Domain.Ports;

public interface IRelacionACargoRepository
{
    /// <summary>Responsable directo vigente de un empleado, o null si no tiene.</summary>
    Task<RelacionACargo?> GetVigenteDeEmpleadoAsync(Guid empleadoId, CancellationToken ct = default);

    /// <summary>Empleados a cargo (relación vigente) de un responsable.</summary>
    Task<IReadOnlyList<RelacionACargo>> GetVigentesDeResponsableAsync(Guid responsableId, CancellationToken ct = default);

    Task<IReadOnlyList<RelacionACargo>> GetVigentesAsync(CancellationToken ct = default);
    Task AddAsync(RelacionACargo relacion, CancellationToken ct = default);
    Task UpdateAsync(RelacionACargo relacion, CancellationToken ct = default);
}