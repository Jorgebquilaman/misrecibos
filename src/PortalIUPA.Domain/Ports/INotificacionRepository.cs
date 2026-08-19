using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Domain.Ports;

public interface INotificacionRepository
{
    Task AddAsync(Notificacion notificacion, CancellationToken ct = default);
    Task<IReadOnlyList<Notificacion>> GetByEmpleadoAsync(Guid empleadoId, CancellationToken ct = default);
    Task<int> GetNoLeidasCountAsync(Guid empleadoId, CancellationToken ct = default);
    Task<Notificacion?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task UpdateAsync(Notificacion notificacion, CancellationToken ct = default);
}