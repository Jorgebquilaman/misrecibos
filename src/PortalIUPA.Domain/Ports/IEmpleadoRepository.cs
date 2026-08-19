using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Enums;

namespace PortalIUPA.Domain.Ports;

public interface IEmpleadoRepository
{
    Task<Empleado?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Empleado?> GetByCorreoAsync(string correo, CancellationToken ct = default);
    Task<Empleado?> GetByLegajoAsync(int legajo, CancellationToken ct = default);
    Task<IReadOnlyList<Empleado>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Empleado>> GetActivosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Empleado>> GetConRolAsync(Rol rol, CancellationToken ct = default);
    Task AddAsync(Empleado empleado, CancellationToken ct = default);
    Task UpdateAsync(Empleado empleado, CancellationToken ct = default);
    Task<bool> ExisteLegajoAsync(int legajo, CancellationToken ct = default);
}