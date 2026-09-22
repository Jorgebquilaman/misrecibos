using Microsoft.EntityFrameworkCore;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Infrastructure.Persistence.Repositories;

public sealed class CvExperienciaAdjuntoRepository : ICvExperienciaAdjuntoRepository
{
    private readonly AppDbContext _db;
    public CvExperienciaAdjuntoRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(CvExperienciaAdjunto adjunto, CancellationToken ct = default)
    {
        _db.CvExperienciaAdjuntos.Add(adjunto);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CvExperienciaAdjunto>> GetByExperienciaAsync(Guid experienciaId, CancellationToken ct = default) =>
        await _db.CvExperienciaAdjuntos.Where(x => x.ExperienciaId == experienciaId).OrderBy(x => x.FechaCarga).ToListAsync(ct);

    public Task<CvExperienciaAdjunto?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.CvExperienciaAdjuntos.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<CvExperienciaAdjunto?> GetByAdjuntoIdAsync(Guid adjuntoId, CancellationToken ct = default) =>
        _db.CvExperienciaAdjuntos.FirstOrDefaultAsync(x => x.AdjuntoId == adjuntoId, ct);

    public async Task DeleteAsync(CvExperienciaAdjunto adjunto, CancellationToken ct = default)
    {
        _db.CvExperienciaAdjuntos.Remove(adjunto);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CvExperienciaAdjunto>> GetByEmpleadoAsync(Guid empleadoId, CancellationToken ct = default)
    {
        var expIds = await _db.CvExperiencias.Where(e => e.EmpleadoId == empleadoId).Select(e => e.Id).ToListAsync(ct);
        if (expIds.Count == 0) return Array.Empty<CvExperienciaAdjunto>();
        return await _db.CvExperienciaAdjuntos.Where(x => expIds.Contains(x.ExperienciaId)).ToListAsync(ct);
    }
}
