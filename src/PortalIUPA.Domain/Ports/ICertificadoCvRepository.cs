using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Domain.Ports;

public interface ICertificadoCvRepository
{
    Task AddAsync(CertificadoCurso certificado, CancellationToken ct = default);

    Task<CertificadoCurso?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<CertificadoCurso>> GetByEmpleadoAsync(Guid empleadoId, CancellationToken ct = default);

    Task<IReadOnlyList<CertificadoCurso>> GetAllAsync(CancellationToken ct = default);

    Task UpdateAsync(CertificadoCurso certificado, CancellationToken ct = default);

    Task DeleteAsync(CertificadoCurso certificado, CancellationToken ct = default);
}
