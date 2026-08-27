using Microsoft.EntityFrameworkCore;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Infrastructure.Persistence.Repositories;

public sealed class CvAntecedenteAcademicoRepository : ICvAntecedenteAcademicoRepository
{
    private readonly AppDbContext _db;

    public CvAntecedenteAcademicoRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(CvAntecedenteAcademico antecedente, CancellationToken ct = default)
    {
        _db.CvAntecedentesAcademicos.Add(antecedente);
        await _db.SaveChangesAsync(ct);
    }

    public Task<CvAntecedenteAcademico?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.CvAntecedentesAcademicos.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<CvAntecedenteAcademico>> GetByEmpleadoAsync(Guid empleadoId, CancellationToken ct = default) =>
        await _db.CvAntecedentesAcademicos.Where(x => x.EmpleadoId == empleadoId).ToListAsync(ct);

    public async Task UpdateAsync(CvAntecedenteAcademico antecedente, CancellationToken ct = default)
    {
        _db.CvAntecedentesAcademicos.Update(antecedente);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(CvAntecedenteAcademico antecedente, CancellationToken ct = default)
    {
        _db.CvAntecedentesAcademicos.Remove(antecedente);
        await _db.SaveChangesAsync(ct);
    }
}
