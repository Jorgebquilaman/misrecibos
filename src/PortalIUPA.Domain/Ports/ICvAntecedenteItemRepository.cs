using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Domain.Ports;

public interface ICvAntecedenteItemRepository
{
    Task AddAsync(CvAntecedenteItem item, CancellationToken ct = default);
    Task<CvAntecedenteItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CvAntecedenteItem>> GetByEmpleadoAsync(Guid empleadoId, CancellationToken ct = default);
    Task UpdateAsync(CvAntecedenteItem item, CancellationToken ct = default);
    Task DeleteAsync(CvAntecedenteItem item, CancellationToken ct = default);
}
