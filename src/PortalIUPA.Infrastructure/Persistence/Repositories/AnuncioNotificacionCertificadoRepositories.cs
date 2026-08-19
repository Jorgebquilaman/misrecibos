using Microsoft.EntityFrameworkCore;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Ports;
using PortalIUPA.Infrastructure.Persistence;

namespace PortalIUPA.Infrastructure.Persistence.Repositories;

public sealed class AnuncioRepository : IAnuncioRepository
{
    private readonly AppDbContext _db;

    public AnuncioRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Anuncio>> GetVigentesParaAsync(Empleado empleado, DateOnly fecha,
        CancellationToken ct = default)
    {
        var vigentes = await _db.Anuncios
            .Where(a => a.Activo &&
                        (a.FechaDesde == null || a.FechaDesde <= fecha) &&
                        (a.FechaHasta == null || a.FechaHasta >= fecha))
            .ToListAsync(ct);

        return vigentes.Where(a => a.AplicaA(empleado)).ToList();
    }

    public async Task<IReadOnlyList<Anuncio>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Anuncios.ToListAsync(ct);

    public Task<Anuncio?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Anuncios.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task AddAsync(Anuncio anuncio, CancellationToken ct = default)
    {
        await _db.Anuncios.AddAsync(anuncio, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Anuncio anuncio, CancellationToken ct = default)
    {
        _db.Anuncios.Update(anuncio);
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class AnuncioLeidoRepository : IAnuncioLeidoRepository
{
    private readonly AppDbContext _db;

    public AnuncioLeidoRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(AnuncioLeido lectura, CancellationToken ct = default)
    {
        await _db.AnunciosLeidos.AddAsync(lectura, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AnuncioLeido>> GetDeEmpleadoAsync(Guid empleadoId, CancellationToken ct = default) =>
        await _db.AnunciosLeidos.Where(l => l.EmpleadoId == empleadoId).ToListAsync(ct);

    public Task<bool> FueLeidoAsync(Guid anuncioId, Guid empleadoId, CancellationToken ct = default) =>
        _db.AnunciosLeidos.AnyAsync(l => l.AnuncioId == anuncioId && l.EmpleadoId == empleadoId, ct);
}

public sealed class NotificacionRepository : INotificacionRepository
{
    private readonly AppDbContext _db;

    public NotificacionRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(Notificacion notificacion, CancellationToken ct = default)
    {
        await _db.Notificaciones.AddAsync(notificacion, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Notificacion>> GetByEmpleadoAsync(Guid empleadoId, CancellationToken ct = default) =>
        await _db.Notificaciones.Where(n => n.EmpleadoId == empleadoId).ToListAsync(ct);

    public Task<int> GetNoLeidasCountAsync(Guid empleadoId, CancellationToken ct = default) =>
        _db.Notificaciones.CountAsync(n => n.EmpleadoId == empleadoId && !n.Leida, ct);

    public Task<Notificacion?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Notificaciones.FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task UpdateAsync(Notificacion notificacion, CancellationToken ct = default)
    {
        _db.Notificaciones.Update(notificacion);
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class CertificadoLaboralRepository : ICertificadoLaboralRepository
{
    private readonly AppDbContext _db;

    public CertificadoLaboralRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(CertificadoLaboral certificado, CancellationToken ct = default)
    {
        await _db.CertificadosLaborales.AddAsync(certificado, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task<CertificadoLaboral?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.CertificadosLaborales.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<CertificadoLaboral>> GetByEmpleadoAsync(Guid empleadoId,
        CancellationToken ct = default) =>
        await _db.CertificadosLaborales.Where(c => c.EmpleadoId == empleadoId).ToListAsync(ct);

    public async Task UpdateAsync(CertificadoLaboral certificado, CancellationToken ct = default)
    {
        _db.CertificadosLaborales.Update(certificado);
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class AccesoLogRepository : IAccesoLogRepository
{
    private readonly AppDbContext _db;

    public AccesoLogRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(AccesoLog log, CancellationToken ct = default)
    {
        await _db.AccesosLog.AddAsync(log, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AccesoLog>> GetAllAsync(CancellationToken ct = default) =>
        await _db.AccesosLog.ToListAsync(ct);

    public async Task<IReadOnlyList<AccesoLog>> GetEntreAsync(DateTime desde, DateTime hasta,
        CancellationToken ct = default) =>
        await _db.AccesosLog.Where(l => l.FechaHora >= desde && l.FechaHora <= hasta).ToListAsync(ct);
}