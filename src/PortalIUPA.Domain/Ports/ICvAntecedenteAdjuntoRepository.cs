using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Domain.Ports;

public interface ICvAntecedenteAdjuntoRepository
{
    Task AddAsync(CvAntecedenteAdjunto adjunto, CancellationToken ct = default);
    Task<IReadOnlyList<CvAntecedenteAdjunto>> GetByAntecedenteAsync(Guid antecedenteId, CancellationToken ct = default);
    Task<CvAntecedenteAdjunto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CvAntecedenteAdjunto?> GetByAdjuntoIdAsync(Guid adjuntoId, CancellationToken ct = default);
    Task DeleteAsync(CvAntecedenteAdjunto adjunto, CancellationToken ct = default);
}
