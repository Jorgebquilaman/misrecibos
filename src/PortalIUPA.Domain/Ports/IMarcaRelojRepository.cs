using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Domain.Ports;

public interface IMarcaRelojRepository
{
    Task AddRangeAsync(IEnumerable<MarcaReloj> marcas, CancellationToken ct = default);
    Task<IReadOnlyList<MarcaReloj>> GetByEmpleadoBetweenAsync(Guid empleadoId, DateTime desde, DateTime hasta,
        CancellationToken ct = default);
    Task<IReadOnlyList<MarcaReloj>> GetByAreasBetweenAsync(IReadOnlyCollection<Guid> areaIds, DateTime desde,
        DateTime hasta, CancellationToken ct = default);
    Task<DateTime?> GetMaxFechaHoraAsync(CancellationToken ct = default);
    Task<bool> ExisteAsync(Guid empleadoId, DateTime fechaHora, CancellationToken ct = default);
}