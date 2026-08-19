using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Domain.Ports;

public interface IAprobacionRepository
{
    Task AddAsync(Aprobacion aprobacion, CancellationToken ct = default);
    Task<IReadOnlyList<Aprobacion>> GetBySolicitudAsync(Guid solicitudId, CancellationToken ct = default);
}