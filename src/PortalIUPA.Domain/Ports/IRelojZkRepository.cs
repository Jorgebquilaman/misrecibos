using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Domain.Ports;

public interface IRelojZkRepository
{
    Task AddAsync(RelojZk reloj, CancellationToken ct = default);
    Task<RelojZk?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<RelojZk>> GetAllAsync(CancellationToken ct = default);
    Task UpdateAsync(RelojZk reloj, CancellationToken ct = default);
    Task DeleteAsync(RelojZk reloj, CancellationToken ct = default);
}

public interface IRelojZkDescargaRepository
{
    Task AddAsync(RelojZkDescarga descarga, CancellationToken ct = default);
    Task<IReadOnlyList<RelojZkDescarga>> GetUltimasAsync(int cantidad = 50, CancellationToken ct = default);
}
