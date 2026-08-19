using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Domain.Ports;

public interface IAccesoLogRepository
{
    Task AddAsync(AccesoLog log, CancellationToken ct = default);
    Task<IReadOnlyList<AccesoLog>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AccesoLog>> GetEntreAsync(DateTime desde, DateTime hasta, CancellationToken ct = default);
}