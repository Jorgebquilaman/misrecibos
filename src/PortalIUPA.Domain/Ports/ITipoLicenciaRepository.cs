using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Domain.Ports;

public interface ITipoLicenciaRepository
{
    Task<IReadOnlyList<TipoLicencia>> GetActivosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TipoLicencia>> GetAllAsync(CancellationToken ct = default);
    Task<TipoLicencia?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(TipoLicencia tipoLicencia, CancellationToken ct = default);
    Task UpdateAsync(TipoLicencia tipoLicencia, CancellationToken ct = default);
}