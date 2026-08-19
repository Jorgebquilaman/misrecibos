using Microsoft.EntityFrameworkCore;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Ports;
using PortalIUPA.Infrastructure.Persistence;

namespace PortalIUPA.Infrastructure.Persistence.Repositories;

public sealed class PeriodoRepository : IPeriodoRepository
{
    private readonly AppDbContext _db;

    public PeriodoRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Periodo>> GetActivosAsync(CancellationToken ct = default) =>
        await _db.Periodos.Where(p => p.Activo).ToListAsync(ct);

    public async Task<IReadOnlyList<Periodo>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Periodos.ToListAsync(ct);

    public Task<Periodo?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Periodos.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Periodo?> GetByCodigoAsync(string codigo, CancellationToken ct = default) =>
        _db.Periodos.FirstOrDefaultAsync(p => p.Codigo == codigo, ct);

    public async Task AddAsync(Periodo periodo, CancellationToken ct = default)
    {
        await _db.Periodos.AddAsync(periodo, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Periodo periodo, CancellationToken ct = default)
    {
        _db.Periodos.Update(periodo);
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class DescargaReciboRepository : IDescargaReciboRepository
{
    private readonly AppDbContext _db;

    public DescargaReciboRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(DescargaRecibo descarga, CancellationToken ct = default)
    {
        await _db.DescargasRecibo.AddAsync(descarga, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DescargaRecibo>> GetByEmpleadoAsync(Guid empleadoId, CancellationToken ct = default) =>
        await _db.DescargasRecibo.Where(d => d.EmpleadoId == empleadoId).ToListAsync(ct);

    public Task<DescargaRecibo?> GetUltimaDeEmpleadoEnPeriodoAsync(Guid empleadoId, Guid periodoId,
        CancellationToken ct = default) =>
        _db.DescargasRecibo
            .Where(d => d.EmpleadoId == empleadoId && d.PeriodoId == periodoId)
            .OrderByDescending(d => d.FechaHora)
            .FirstOrDefaultAsync(ct);
}

public sealed class TipoLicenciaRepository : ITipoLicenciaRepository
{
    private readonly AppDbContext _db;

    public TipoLicenciaRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<TipoLicencia>> GetActivosAsync(CancellationToken ct = default) =>
        await _db.TiposLicencia.Where(t => t.Activo).ToListAsync(ct);

    public async Task<IReadOnlyList<TipoLicencia>> GetAllAsync(CancellationToken ct = default) =>
        await _db.TiposLicencia.ToListAsync(ct);

    public Task<TipoLicencia?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.TiposLicencia.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task AddAsync(TipoLicencia tipoLicencia, CancellationToken ct = default)
    {
        await _db.TiposLicencia.AddAsync(tipoLicencia, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(TipoLicencia tipoLicencia, CancellationToken ct = default)
    {
        _db.TiposLicencia.Update(tipoLicencia);
        await _db.SaveChangesAsync(ct);
    }
}