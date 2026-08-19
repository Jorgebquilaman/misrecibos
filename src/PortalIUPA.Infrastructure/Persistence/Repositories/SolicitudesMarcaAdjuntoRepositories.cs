using Microsoft.EntityFrameworkCore;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.Ports;
using PortalIUPA.Domain.ValueObjects;
using PortalIUPA.Infrastructure.Persistence;

namespace PortalIUPA.Infrastructure.Persistence.Repositories;

public sealed class SolicitudLicenciaRepository : ISolicitudLicenciaRepository
{
    private readonly AppDbContext _db;

    public SolicitudLicenciaRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(SolicitudLicencia solicitud, CancellationToken ct = default)
    {
        await _db.SolicitudesLicencia.AddAsync(solicitud, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task<SolicitudLicencia?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.SolicitudesLicencia.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<SolicitudLicencia>> GetByEmpleadoAsync(Guid empleadoId, CancellationToken ct = default) =>
        await _db.SolicitudesLicencia.Where(s => s.EmpleadoId == empleadoId).ToListAsync(ct);

    public async Task<IReadOnlyList<SolicitudLicencia>> GetEnEsperaAsync(CancellationToken ct = default) =>
        await _db.SolicitudesLicencia.Where(s => s.Estado == EstadoSolicitud.EnEspera).ToListAsync(ct);

    public async Task<IReadOnlyList<SolicitudLicencia>> GetAllAsync(CancellationToken ct = default) =>
        await _db.SolicitudesLicencia.ToListAsync(ct);

    public async Task<IReadOnlyList<SolicitudLicencia>> GetSolapadasAsync(Guid empleadoId, Guid tipoLicenciaId,
        RangoFechas rango, CancellationToken ct = default) =>
        await _db.SolicitudesLicencia
            .Where(s => s.EmpleadoId == empleadoId &&
                        s.TipoLicenciaId == tipoLicenciaId &&
                        s.Estado != EstadoSolicitud.Cancelada &&
                        s.Estado != EstadoSolicitud.Desaprobada &&
                        s.FechaInicio <= rango.Fin &&
                        s.FechaFin >= rango.Inicio)
            .ToListAsync(ct);

    public async Task<(int DiasMes, int DiasAnio)> GetConsumoAsync(Guid empleadoId, Guid tipoLicenciaId, int anio,
        int mes, CancellationToken ct = default)
    {
        var solicitudes = await _db.SolicitudesLicencia
            .Where(s => s.EmpleadoId == empleadoId &&
                        s.TipoLicenciaId == tipoLicenciaId &&
                        (s.Estado == EstadoSolicitud.Aprobada || s.Estado == EstadoSolicitud.EnEspera))
            .ToListAsync(ct);

        var diasMes = 0;
        var diasAnio = 0;
        foreach (var solicitud in solicitudes)
        {
            var rango = solicitud.Rango;
            diasMes += rango.DiasDentroDelMes(anio, mes);
            diasAnio += rango.DiasDentroDelAnio(anio);
        }

        return (diasMes, diasAnio);
    }

    public async Task UpdateAsync(SolicitudLicencia solicitud, CancellationToken ct = default)
    {
        _db.SolicitudesLicencia.Update(solicitud);
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class AprobacionRepository : IAprobacionRepository
{
    private readonly AppDbContext _db;

    public AprobacionRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(Aprobacion aprobacion, CancellationToken ct = default)
    {
        await _db.Aprobaciones.AddAsync(aprobacion, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Aprobacion>> GetBySolicitudAsync(Guid solicitudId, CancellationToken ct = default) =>
        await _db.Aprobaciones.Where(a => a.SolicitudId == solicitudId).ToListAsync(ct);
}

public sealed class AdjuntoRepository : IAdjuntoRepository
{
    private readonly AppDbContext _db;

    public AdjuntoRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(Adjunto adjunto, CancellationToken ct = default)
    {
        await _db.Adjuntos.AddAsync(adjunto, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task<Adjunto?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Adjuntos.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Adjunto>> GetByEntidadAsync(string entidadTipo, Guid entidadId,
        CancellationToken ct = default) =>
        await _db.Adjuntos.Where(a => a.EntidadTipo == entidadTipo && a.EntidadId == entidadId).ToListAsync(ct);

    public async Task ActualizarAsync(Adjunto adjunto, CancellationToken ct = default)
    {
        _db.Adjuntos.Update(adjunto);
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class MarcaRelojRepository : IMarcaRelojRepository
{
    private readonly AppDbContext _db;

    public MarcaRelojRepository(AppDbContext db) => _db = db;

    public async Task AddRangeAsync(IEnumerable<MarcaReloj> marcas, CancellationToken ct = default)
    {
        await _db.MarcasReloj.AddRangeAsync(marcas, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<MarcaReloj>> GetByEmpleadoBetweenAsync(Guid empleadoId, DateTime desde,
        DateTime hasta, CancellationToken ct = default) =>
        await _db.MarcasReloj
            .Where(m => m.EmpleadoId == empleadoId && m.FechaHora >= desde && m.FechaHora <= hasta)
            .OrderBy(m => m.FechaHora)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<MarcaReloj>> GetByAreasBetweenAsync(IReadOnlyCollection<Guid> areaIds,
        DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        if (areaIds.Count == 0)
            return new List<MarcaReloj>();

        return await _db.MarcasReloj
            .Join(_db.Empleados, m => m.EmpleadoId, e => e.Id, (m, e) => new { Marca = m, Empleado = e })
            .Where(x => x.Empleado.AreaId != null && areaIds.Contains(x.Empleado.AreaId.Value) &&
                        x.Marca.FechaHora >= desde && x.Marca.FechaHora <= hasta)
            .Select(x => x.Marca)
            .OrderBy(m => m.FechaHora)
            .ToListAsync(ct);
    }

    public async Task<DateTime?> GetMaxFechaHoraAsync(CancellationToken ct = default) =>
        await _db.MarcasReloj.MaxAsync(m => (DateTime?)m.FechaHora, ct);

    public Task<bool> ExisteAsync(Guid empleadoId, DateTime fechaHora, CancellationToken ct = default) =>
        _db.MarcasReloj.AnyAsync(m => m.EmpleadoId == empleadoId && m.FechaHora == fechaHora, ct);
}