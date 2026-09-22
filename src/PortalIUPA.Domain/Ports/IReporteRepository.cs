using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Domain.Ports;

public interface IReporteRepository
{
    Task AddAsync(ReporteDefinicion reporte, CancellationToken ct = default);
    Task<ReporteDefinicion?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ReporteDefinicion>> GetAllAsync(CancellationToken ct = default);
    Task UpdateAsync(ReporteDefinicion reporte, CancellationToken ct = default);
    Task DeleteAsync(ReporteDefinicion reporte, CancellationToken ct = default);
}

public interface IDashboardRepository
{
    Task AddAsync(DashboardDefinicion dashboard, CancellationToken ct = default);
    Task<DashboardDefinicion?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DashboardDefinicion>> GetAllAsync(CancellationToken ct = default);
    Task UpdateAsync(DashboardDefinicion dashboard, CancellationToken ct = default);
    Task DeleteAsync(DashboardDefinicion dashboard, CancellationToken ct = default);
}
