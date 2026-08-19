namespace PortalIUPA.Domain.Ports;

public interface IAnuncioLeidoRepository
{
    Task AddAsync(Entities.AnuncioLeido lectura, CancellationToken ct = default);
    Task<IReadOnlyList<Entities.AnuncioLeido>> GetDeEmpleadoAsync(Guid empleadoId, CancellationToken ct = default);
    Task<bool> FueLeidoAsync(Guid anuncioId, Guid empleadoId, CancellationToken ct = default);
}