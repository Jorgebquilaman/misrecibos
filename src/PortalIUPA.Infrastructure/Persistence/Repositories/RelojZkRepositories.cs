using Microsoft.EntityFrameworkCore;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Infrastructure.Persistence.Repositories;

public sealed class RelojZkRepository : IRelojZkRepository
{
    private readonly AppDbContext _db;
    public RelojZkRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(RelojZk reloj, CancellationToken ct = default)
    {
        _db.RelojesZk.Add(reloj);
        await _db.SaveChangesAsync(ct);
    }

    public Task<RelojZk?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.RelojesZk.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<RelojZk>> GetAllAsync(CancellationToken ct = default) =>
        await _db.RelojesZk.OrderBy(x => x.Nombre).ToListAsync(ct);

    public async Task UpdateAsync(RelojZk reloj, CancellationToken ct = default)
    {
        _db.RelojesZk.Update(reloj);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(RelojZk reloj, CancellationToken ct = default)
    {
        _db.RelojesZk.Remove(reloj);
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class RelojZkDescargaRepository : IRelojZkDescargaRepository
{
    private readonly AppDbContext _db;
    public RelojZkDescargaRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(RelojZkDescarga descarga, CancellationToken ct = default)
    {
        _db.RelojesZkDescargas.Add(descarga);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<RelojZkDescarga>> GetUltimasAsync(int cantidad = 50, CancellationToken ct = default) =>
        await _db.RelojesZkDescargas.OrderByDescending(x => x.Fecha).Take(cantidad).ToListAsync(ct);
}
