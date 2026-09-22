using Microsoft.EntityFrameworkCore;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Infrastructure.Persistence.Repositories;

public sealed class CvAntecedenteItemRepository : ICvAntecedenteItemRepository
{
    private readonly AppDbContext _db;
    public CvAntecedenteItemRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(CvAntecedenteItem item, CancellationToken ct = default)
    {
        _db.CvAntecedenteItems.Add(item);
        await _db.SaveChangesAsync(ct);
    }

    public Task<CvAntecedenteItem?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.CvAntecedenteItems.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<CvAntecedenteItem>> GetByEmpleadoAsync(Guid empleadoId, CancellationToken ct = default) =>
        await _db.CvAntecedenteItems
            .Where(x => x.EmpleadoId == empleadoId)
            .OrderBy(x => x.Seccion).ThenBy(x => x.Categoria).ThenByDescending(x => x.FechaDesde)
            .ToListAsync(ct);

    public async Task UpdateAsync(CvAntecedenteItem item, CancellationToken ct = default)
    {
        _db.CvAntecedenteItems.Update(item);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(CvAntecedenteItem item, CancellationToken ct = default)
    {
        _db.CvAntecedenteItems.Remove(item);
        await _db.SaveChangesAsync(ct);
    }
}
