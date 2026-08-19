using Microsoft.EntityFrameworkCore;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.Ports;
using PortalIUPA.Domain.ValueObjects;
using PortalIUPA.Infrastructure.Persistence;

namespace PortalIUPA.Infrastructure.Persistence.Repositories;

public sealed class EmpleadoRepository : IEmpleadoRepository
{
    private readonly AppDbContext _db;

    public EmpleadoRepository(AppDbContext db) => _db = db;

    public Task<Empleado?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Empleados.FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<Empleado?> GetByCorreoAsync(string correo, CancellationToken ct = default) =>
        _db.Empleados.FirstOrDefaultAsync(e => e.Correo == new Email(correo), ct);

    public Task<Empleado?> GetByLegajoAsync(int legajo, CancellationToken ct = default) =>
        _db.Empleados.FirstOrDefaultAsync(e => e.Legajo == legajo, ct);

    public async Task<IReadOnlyList<Empleado>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Empleados.OrderBy(e => e.Apellido).ThenBy(e => e.Nombre).ToListAsync(ct);

    public async Task<IReadOnlyList<Empleado>> GetActivosAsync(CancellationToken ct = default) =>
        await _db.Empleados.Where(e => e.Activo).OrderBy(e => e.Apellido).ToListAsync(ct);

    public async Task<IReadOnlyList<Empleado>> GetConRolAsync(Rol rol, CancellationToken ct = default) =>
        await _db.Empleados.Where(e => e.Activo && e.Roles.Contains(rol)).ToListAsync(ct);

    public async Task AddAsync(Empleado empleado, CancellationToken ct = default)
    {
        await _db.Empleados.AddAsync(empleado, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Empleado empleado, CancellationToken ct = default)
    {
        _db.Empleados.Update(empleado);
        await _db.SaveChangesAsync(ct);
    }

    public Task<bool> ExisteLegajoAsync(int legajo, CancellationToken ct = default) =>
        _db.Empleados.AnyAsync(e => e.Legajo == legajo, ct);
}

public sealed class AreaRepository : IAreaRepository
{
    private readonly AppDbContext _db;

    public AreaRepository(AppDbContext db) => _db = db;

    public Task<Area?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Areas.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Area>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Areas.OrderBy(a => a.Nombre).ToListAsync(ct);

    public async Task AddAsync(Area area, CancellationToken ct = default)
    {
        await _db.Areas.AddAsync(area, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Area area, CancellationToken ct = default)
    {
        _db.Areas.Update(area);
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class RelacionACargoRepository : IRelacionACargoRepository
{
    private readonly AppDbContext _db;

    public RelacionACargoRepository(AppDbContext db) => _db = db;

    public Task<RelacionACargo?> GetVigenteDeEmpleadoAsync(Guid empleadoId, CancellationToken ct = default) =>
        _db.RelacionesACargo.FirstOrDefaultAsync(r =>
            r.EmpleadoId == empleadoId && r.FechaHasta == null, ct);

    public async Task<IReadOnlyList<RelacionACargo>> GetVigentesDeResponsableAsync(Guid responsableId,
        CancellationToken ct = default) =>
        await _db.RelacionesACargo
            .Where(r => r.ResponsableId == responsableId && r.FechaHasta == null)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<RelacionACargo>> GetVigentesAsync(CancellationToken ct = default) =>
        await _db.RelacionesACargo.Where(r => r.FechaHasta == null).ToListAsync(ct);

    public async Task AddAsync(RelacionACargo relacion, CancellationToken ct = default)
    {
        await _db.RelacionesACargo.AddAsync(relacion, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(RelacionACargo relacion, CancellationToken ct = default)
    {
        _db.RelacionesACargo.Update(relacion);
        await _db.SaveChangesAsync(ct);
    }
}