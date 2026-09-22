using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Domain.Ports;

public interface ICvItemAdjuntoRepository
{
    Task AddAsync(CvAntecedenteItemAdjunto adjunto, CancellationToken ct = default);
    Task<IReadOnlyList<CvAntecedenteItemAdjunto>> GetByItemAsync(Guid itemId, CancellationToken ct = default);
    Task<CvAntecedenteItemAdjunto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CvAntecedenteItemAdjunto?> GetByAdjuntoIdAsync(Guid adjuntoId, CancellationToken ct = default);
    Task DeleteAsync(CvAntecedenteItemAdjunto adjunto, CancellationToken ct = default);
}
