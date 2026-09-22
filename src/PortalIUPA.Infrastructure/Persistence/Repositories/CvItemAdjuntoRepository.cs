using Microsoft.EntityFrameworkCore;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Infrastructure.Persistence.Repositories;

public sealed class CvItemAdjuntoRepository : ICvItemAdjuntoRepository
{
    private readonly AppDbContext _db;
    public CvItemAdjuntoRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(CvAntecedenteItemAdjunto adjunto, CancellationToken ct = default)
    {
        _db.CvItemAdjuntos.Add(adjunto);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CvAntecedenteItemAdjunto>> GetByItemAsync(Guid itemId, CancellationToken ct = default) =>
        await _db.CvItemAdjuntos.Where(x => x.ItemId == itemId).OrderBy(x => x.FechaCarga).ToListAsync(ct);

    public Task<CvAntecedenteItemAdjunto?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.CvItemAdjuntos.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<CvAntecedenteItemAdjunto?> GetByAdjuntoIdAsync(Guid adjuntoId, CancellationToken ct = default) =>
        _db.CvItemAdjuntos.FirstOrDefaultAsync(x => x.AdjuntoId == adjuntoId, ct);

    public async Task DeleteAsync(CvAntecedenteItemAdjunto adjunto, CancellationToken ct = default)
    {
        _db.CvItemAdjuntos.Remove(adjunto);
        await _db.SaveChangesAsync(ct);
    }
}
