using Microsoft.EntityFrameworkCore;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Ports;
using PortalIUPA.Infrastructure.Persistence;

namespace PortalIUPA.Infrastructure.Persistence.Repositories;

public sealed class CertificadoCvRepository : ICertificadoCvRepository
{
    private readonly AppDbContext _db;

    public CertificadoCvRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(CertificadoCurso certificado, CancellationToken ct = default)
    {
        await _db.CertificadosCv.AddAsync(certificado, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task<CertificadoCurso?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.CertificadosCv.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<CertificadoCurso>> GetByEmpleadoAsync(Guid empleadoId, CancellationToken ct = default) =>
        await _db.CertificadosCv.Where(c => c.EmpleadoId == empleadoId).ToListAsync(ct);

    public async Task<IReadOnlyList<CertificadoCurso>> GetAllAsync(CancellationToken ct = default) =>
        await _db.CertificadosCv.ToListAsync(ct);

    public async Task UpdateAsync(CertificadoCurso certificado, CancellationToken ct = default)
    {
        _db.CertificadosCv.Update(certificado);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(CertificadoCurso certificado, CancellationToken ct = default)
    {
        _db.CertificadosCv.Remove(certificado);
        await _db.SaveChangesAsync(ct);
    }
}
