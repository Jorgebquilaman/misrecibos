using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Domain.Ports;

public interface ICertificadoLaboralRepository
{
    Task AddAsync(CertificadoLaboral certificado, CancellationToken ct = default);
    Task<CertificadoLaboral?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CertificadoLaboral>> GetByEmpleadoAsync(Guid empleadoId, CancellationToken ct = default);
    Task UpdateAsync(CertificadoLaboral certificado, CancellationToken ct = default);
}