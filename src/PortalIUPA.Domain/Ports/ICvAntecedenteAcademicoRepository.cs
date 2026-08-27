using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Domain.Ports;

public interface ICvAntecedenteAcademicoRepository
{
    Task AddAsync(CvAntecedenteAcademico antecedente, CancellationToken ct = default);

    Task<CvAntecedenteAcademico?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<CvAntecedenteAcademico>> GetByEmpleadoAsync(Guid empleadoId, CancellationToken ct = default);

    Task UpdateAsync(CvAntecedenteAcademico antecedente, CancellationToken ct = default);

    Task DeleteAsync(CvAntecedenteAcademico antecedente, CancellationToken ct = default);
}
