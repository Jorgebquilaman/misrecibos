using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Domain.Ports;

public interface IDescargaReciboRepository
{
    Task AddAsync(DescargaRecibo descarga, CancellationToken ct = default);
    Task<IReadOnlyList<DescargaRecibo>> GetByEmpleadoAsync(Guid empleadoId, CancellationToken ct = default);
    Task<DescargaRecibo?> GetUltimaDeEmpleadoEnPeriodoAsync(Guid empleadoId, Guid periodoId, CancellationToken ct = default);
}