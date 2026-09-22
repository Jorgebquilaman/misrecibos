using Microsoft.EntityFrameworkCore;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Infrastructure.Persistence.Repositories;

public sealed class CvAntecedenteAdjuntoRepository : ICvAntecedenteAdjuntoRepository
{
    private readonly AppDbContext _db;
    public CvAntecedenteAdjuntoRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(CvAntecedenteAdjunto adjunto, CancellationToken ct = default)
    {
        _db.CvAntecedenteAdjuntos.Add(adjunto);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CvAntecedenteAdjunto>> GetByAntecedenteAsync(Guid antecedenteId, CancellationToken ct = default) =>
        await _db.CvAntecedenteAdjuntos.Where(x => x.AntecedenteId == antecedenteId).OrderBy(x => x.FechaCarga).ToListAsync(ct);

    public Task<CvAntecedenteAdjunto?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.CvAntecedenteAdjuntos.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<CvAntecedenteAdjunto?> GetByAdjuntoIdAsync(Guid adjuntoId, CancellationToken ct = default) =>
        _db.CvAntecedenteAdjuntos.FirstOrDefaultAsync(x => x.AdjuntoId == adjuntoId, ct);

    public async Task DeleteAsync(CvAntecedenteAdjunto adjunto, CancellationToken ct = default)
    {
        _db.CvAntecedenteAdjuntos.Remove(adjunto);
        await _db.SaveChangesAsync(ct);
    }
}
