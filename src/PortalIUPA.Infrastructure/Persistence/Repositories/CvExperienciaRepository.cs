using Microsoft.EntityFrameworkCore;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Ports;
using PortalIUPA.Infrastructure.Persistence;

namespace PortalIUPA.Infrastructure.Persistence.Repositories;

public sealed class CvExperienciaRepository : ICvExperienciaRepository
{
    private readonly AppDbContext _db;

    public CvExperienciaRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(CvExperiencia experiencia, CancellationToken ct = default)
    {
        await _db.CvExperiencias.AddAsync(experiencia, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task<CvExperiencia?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.CvExperiencias.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<CvExperiencia>> GetByEmpleadoAsync(Guid empleadoId, CancellationToken ct = default) =>
        await _db.CvExperiencias.Where(e => e.EmpleadoId == empleadoId).ToListAsync(ct);

    public async Task UpdateAsync(CvExperiencia experiencia, CancellationToken ct = default)
    {
        _db.CvExperiencias.Update(experiencia);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(CvExperiencia experiencia, CancellationToken ct = default)
    {
        _db.CvExperiencias.Remove(experiencia);
        await _db.SaveChangesAsync(ct);
    }
}
