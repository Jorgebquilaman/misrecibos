using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Domain.Ports;

public interface IAnuncioRepository
{
    Task<IReadOnlyList<Anuncio>> GetVigentesParaAsync(Empleado empleado, DateOnly fecha, CancellationToken ct = default);
    Task<IReadOnlyList<Anuncio>> GetAllAsync(CancellationToken ct = default);
    Task<Anuncio?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Anuncio anuncio, CancellationToken ct = default);
    Task UpdateAsync(Anuncio anuncio, CancellationToken ct = default);
}