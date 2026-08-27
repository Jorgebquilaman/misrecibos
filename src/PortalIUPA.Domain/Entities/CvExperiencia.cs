namespace PortalIUPA.Domain.Entities;

/// <summary>Experiencia laboral declarada por el empleado para su CV.</summary>
public sealed class CvExperiencia
{
    public Guid Id { get; private set; }
    public Guid EmpleadoId { get; private set; }
    public string Puesto { get; private set; } = null!;
    public string Institucion { get; private set; } = null!;
    public string? Descripcion { get; private set; }
    public DateOnly FechaDesde { get; private set; }
    public DateOnly? FechaHasta { get; private set; }

    private CvExperiencia() { }

    public CvExperiencia(Guid empleadoId, string puesto, string institucion, string? descripcion,
        DateOnly fechaDesde, DateOnly? fechaHasta)
    {
        if (string.IsNullOrWhiteSpace(puesto)) throw new ArgumentException("El puesto es obligatorio.", nameof(puesto));
        if (string.IsNullOrWhiteSpace(institucion)) throw new ArgumentException("La institución es obligatoria.", nameof(institucion));
        if (fechaHasta is { } hasta && hasta < fechaDesde)
            throw new ArgumentException("La fecha de fin no puede ser anterior a la de inicio.", nameof(fechaHasta));

        Id = Guid.NewGuid();
        EmpleadoId = empleadoId;
        Puesto = puesto.Trim();
        Institucion = institucion.Trim();
        Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        FechaDesde = fechaDesde;
        FechaHasta = fechaHasta;
    }

    /// <summary>Edita todos los campos de la experiencia.</summary>
    public void Editar(string puesto, string institucion, string? descripcion, DateOnly fechaDesde, DateOnly? fechaHasta)
    {
        if (string.IsNullOrWhiteSpace(puesto)) throw new ArgumentException("El puesto es obligatorio.", nameof(puesto));
        if (string.IsNullOrWhiteSpace(institucion)) throw new ArgumentException("La institución es obligatoria.", nameof(institucion));
        if (fechaHasta is { } hasta && hasta < fechaDesde)
            throw new ArgumentException("La fecha de fin no puede ser anterior a la de inicio.", nameof(fechaHasta));

        Puesto = puesto.Trim();
        Institucion = institucion.Trim();
        Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        FechaDesde = fechaDesde;
        FechaHasta = fechaHasta;
    }
}
