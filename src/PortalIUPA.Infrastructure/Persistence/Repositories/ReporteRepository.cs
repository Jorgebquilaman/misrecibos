using Microsoft.EntityFrameworkCore;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Ports;
using PortalIUPA.Infrastructure.Persistence;

namespace PortalIUPA.Infrastructure.Persistence.Repositories;

public sealed class ReporteRepository : IReporteRepository
{
    private readonly AppDbContext _db;

    public ReporteRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(ReporteDefinicion reporte, CancellationToken ct = default)
    {
        await _db.Reportes.AddAsync(reporte, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task<ReporteDefinicion?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Reportes.Include(r => r.Permisos).FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<ReporteDefinicion>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Reportes.Include(r => r.Permisos).OrderBy(r => r.Nombre).ToListAsync(ct);

    public async Task UpdateAsync(ReporteDefinicion reporte, CancellationToken ct = default)
    {
        _db.Reportes.Update(reporte);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(ReporteDefinicion reporte, CancellationToken ct = default)
    {
        _db.Reportes.Remove(reporte);
        await _db.SaveChangesAsync(ct);
    }
}
