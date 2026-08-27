namespace PortalIUPA.Domain.Entities;

/// <summary>Nivel del antecedente académico.</summary>
public enum NivelAcademico
{
    Secundario = 1,
    Terciario = 2,
    Universitario = 3,
    Posgrado = 4,
    Maestria = 5,
    Doctorado = 6,
    Otro = 7
}

/// <summary>Antecedente académico declarado por el empleado para su CV, con título escaneado adjunto.</summary>
public sealed class CvAntecedenteAcademico
{
    public Guid Id { get; private set; }
    public Guid EmpleadoId { get; private set; }
    public string Titulo { get; private set; } = null!;
    public string Institucion { get; private set; } = null!;
    public NivelAcademico Nivel { get; private set; }
    public string? Descripcion { get; private set; }
    public DateOnly FechaDesde { get; private set; }
    public DateOnly? FechaHasta { get; private set; }
    public Guid AdjuntoId { get; private set; }
    public DateTime FechaCarga { get; private set; }

    private CvAntecedenteAcademico() { }

    public CvAntecedenteAcademico(Guid empleadoId, string titulo, string institucion, NivelAcademico nivel,
        string? descripcion, DateOnly fechaDesde, DateOnly? fechaHasta, Guid adjuntoId)
    {
        if (string.IsNullOrWhiteSpace(titulo)) throw new ArgumentException("El título es obligatorio.", nameof(titulo));
        if (string.IsNullOrWhiteSpace(institucion)) throw new ArgumentException("La institución es obligatoria.", nameof(institucion));
        if (fechaHasta is { } hasta && hasta < fechaDesde)
            throw new ArgumentException("La fecha de fin no puede ser anterior a la de inicio.", nameof(fechaHasta));

        Id = Guid.NewGuid();
        EmpleadoId = empleadoId;
        Titulo = titulo.Trim();
        Institucion = institucion.Trim();
        Nivel = nivel;
        Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        FechaDesde = fechaDesde;
        FechaHasta = fechaHasta;
        AdjuntoId = adjuntoId;
        FechaCarga = DateTime.UtcNow;
    }

    public void Editar(string titulo, string institucion, NivelAcademico nivel, string? descripcion,
        DateOnly fechaDesde, DateOnly? fechaHasta)
    {
        if (string.IsNullOrWhiteSpace(titulo)) throw new ArgumentException("El título es obligatorio.", nameof(titulo));
        if (string.IsNullOrWhiteSpace(institucion)) throw new ArgumentException("La institución es obligatoria.", nameof(institucion));
        if (fechaHasta is { } hasta && hasta < fechaDesde)
            throw new ArgumentException("La fecha de fin no puede ser anterior a la de inicio.", nameof(fechaHasta));

        Titulo = titulo.Trim();
        Institucion = institucion.Trim();
        Nivel = nivel;
        Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        FechaDesde = fechaDesde;
        FechaHasta = fechaHasta;
    }

    public void ActualizarAdjunto(Guid nuevoAdjuntoId) => AdjuntoId = nuevoAdjuntoId;
}
