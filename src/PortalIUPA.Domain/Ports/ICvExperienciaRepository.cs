using PortalIUPA.Domain.Entities;

namespace PortalIUPA.Domain.Ports;

public interface ICvExperienciaRepository
{
    Task AddAsync(CvExperiencia experiencia, CancellationToken ct = default);

    Task<CvExperiencia?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<CvExperiencia>> GetByEmpleadoAsync(Guid empleadoId, CancellationToken ct = default);

    Task UpdateAsync(CvExperiencia experiencia, CancellationToken ct = default);

    Task DeleteAsync(CvExperiencia experiencia, CancellationToken ct = default);
}
