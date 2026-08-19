using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Domain.Ports;

public interface IAreaRepository
{
    Task<Area?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Area>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Area area, CancellationToken ct = default);
    Task UpdateAsync(Area area, CancellationToken ct = default);
}