using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Domain.Ports;

public interface IPeriodoRepository
{
    Task<IReadOnlyList<Periodo>> GetActivosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Periodo>> GetAllAsync(CancellationToken ct = default);
    Task<Periodo?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Periodo?> GetByCodigoAsync(string codigo, CancellationToken ct = default);
    Task AddAsync(Periodo periodo, CancellationToken ct = default);
    Task UpdateAsync(Periodo periodo, CancellationToken ct = default);
}