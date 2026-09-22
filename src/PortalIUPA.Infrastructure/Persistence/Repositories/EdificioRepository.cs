using Microsoft.EntityFrameworkCore;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Infrastructure.Persistence.Repositories;

public sealed class EdificioRepository : IEdificioRepository
{
    private readonly AppDbContext _db;
    public EdificioRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(Edificio edificio, CancellationToken ct = default)
    {
        _db.Edificios.Add(edificio);
        await _db.SaveChangesAsync(ct);
    }

    public Task<Edificio?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Edificios.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<Edificio>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Edificios.OrderBy(x => x.Nombre).ToListAsync(ct);

    public async Task UpdateAsync(Edificio edificio, CancellationToken ct = default)
    {
        _db.Edificios.Update(edificio);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Edificio edificio, CancellationToken ct = default)
    {
        _db.Edificios.Remove(edificio);
        await _db.SaveChangesAsync(ct);
    }
}
