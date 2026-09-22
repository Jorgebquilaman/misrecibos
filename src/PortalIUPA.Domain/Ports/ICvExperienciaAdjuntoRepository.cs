using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Domain.Ports;

public interface ICvExperienciaAdjuntoRepository
{
    Task AddAsync(CvExperienciaAdjunto adjunto, CancellationToken ct = default);
    Task<IReadOnlyList<CvExperienciaAdjunto>> GetByExperienciaAsync(Guid experienciaId, CancellationToken ct = default);
    Task<CvExperienciaAdjunto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CvExperienciaAdjunto?> GetByAdjuntoIdAsync(Guid adjuntoId, CancellationToken ct = default);
    Task DeleteAsync(CvExperienciaAdjunto adjunto, CancellationToken ct = default);
    Task<IReadOnlyList<CvExperienciaAdjunto>> GetByEmpleadoAsync(Guid empleadoId, CancellationToken ct = default);
}
