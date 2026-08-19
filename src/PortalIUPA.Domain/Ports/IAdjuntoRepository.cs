using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Domain.Ports;

public interface IAdjuntoRepository
{
    Task AddAsync(Adjunto adjunto, CancellationToken ct = default);
    Task<Adjunto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Adjunto>> GetByEntidadAsync(string entidadTipo, Guid entidadId, CancellationToken ct = default);
    Task ActualizarAsync(Adjunto adjunto, CancellationToken ct = default);
}