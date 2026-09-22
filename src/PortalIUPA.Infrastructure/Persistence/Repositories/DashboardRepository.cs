using Microsoft.EntityFrameworkCore;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Ports;
using PortalIUPA.Infrastructure.Persistence;

namespace PortalIUPA.Infrastructure.Persistence.Repositories;

public sealed class DashboardRepository : IDashboardRepository
{
    private readonly AppDbContext _db;

    public DashboardRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(DashboardDefinicion dashboard, CancellationToken ct = default)
    {
        await _db.Dashboards.AddAsync(dashboard, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task<DashboardDefinicion?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Dashboards.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<DashboardDefinicion>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Dashboards.OrderBy(d => d.Nombre).ToListAsync(ct);

    public async Task UpdateAsync(DashboardDefinicion dashboard, CancellationToken ct = default)
    {
        _db.Dashboards.Update(dashboard);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(DashboardDefinicion dashboard, CancellationToken ct = default)
    {
        _db.Dashboards.Remove(dashboard);
        await _db.SaveChangesAsync(ct);
    }
}
