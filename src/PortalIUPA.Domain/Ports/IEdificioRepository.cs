using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Domain.Ports;

public interface IEdificioRepository
{
    Task AddAsync(Edificio edificio, CancellationToken ct = default);
    Task<Edificio?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Edificio>> GetAllAsync(CancellationToken ct = default);
    Task UpdateAsync(Edificio edificio, CancellationToken ct = default);
    Task DeleteAsync(Edificio edificio, CancellationToken ct = default);
}
