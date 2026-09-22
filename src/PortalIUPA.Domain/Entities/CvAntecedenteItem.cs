namespace PortalIUPA.Domain.Entities;

/// <summary>Sección del CV a la que pertenece un ítem de antecedente/producción.</summary>
public enum SeccionCvItem
{
    AntecedentesProfArtisticos = 1,
    Produccion = 2,
    OtrosAntecedentes = 3
}

/// <summary>
/// Ítem de antecedente profesional/artístico, producción u otro antecedente declarado por el empleado
/// para su CV (categoría + datos + adjuntos múltiples).
/// </summary>
public sealed class CvAntecedenteItem
{
    public Guid Id { get; private set; }
    public Guid EmpleadoId { get; private set; }
    public SeccionCvItem Seccion { get; private set; }
    public string Categoria { get; private set; } = null!;
    public string Titulo { get; private set; } = null!;
    public string? Institucion { get; private set; }
    public string? Descripcion { get; private set; }
    public DateOnly FechaDesde { get; private set; }
    public DateOnly? FechaHasta { get; private set; }
    public DateTime FechaCarga { get; private set; }

    private CvAntecedenteItem() { }

    public CvAntecedenteItem(Guid empleadoId, SeccionCvItem seccion, string categoria, string titulo,
        string? institucion, string? descripcion, DateOnly fechaDesde, DateOnly? fechaHasta)
    {
        if (string.IsNullOrWhiteSpace(categoria)) throw new ArgumentException("La categoría es obligatoria.", nameof(categoria));
        if (string.IsNullOrWhiteSpace(titulo)) throw new ArgumentException("El título es obligatorio.", nameof(titulo));
        if (fechaHasta is { } hasta && hasta < fechaDesde)
            throw new ArgumentException("La fecha de fin no puede ser anterior a la de inicio.", nameof(fechaHasta));

        Id = Guid.NewGuid();
        EmpleadoId = empleadoId;
        Seccion = seccion;
        Categoria = categoria.Trim();
        Titulo = titulo.Trim();
        Institucion = string.IsNullOrWhiteSpace(institucion) ? null : institucion.Trim();
        Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        FechaDesde = fechaDesde;
        FechaHasta = fechaHasta;
        FechaCarga = DateTime.UtcNow;
    }

    public void Editar(SeccionCvItem seccion, string categoria, string titulo, string? institucion,
        string? descripcion, DateOnly fechaDesde, DateOnly? fechaHasta)
    {
        if (string.IsNullOrWhiteSpace(categoria)) throw new ArgumentException("La categoría es obligatoria.", nameof(categoria));
        if (string.IsNullOrWhiteSpace(titulo)) throw new ArgumentException("El título es obligatorio.", nameof(titulo));
        if (fechaHasta is { } hasta && hasta < fechaDesde)
            throw new ArgumentException("La fecha de fin no puede ser anterior a la de inicio.", nameof(fechaHasta));

        Seccion = seccion;
        Categoria = categoria.Trim();
        Titulo = titulo.Trim();
        Institucion = string.IsNullOrWhiteSpace(institucion) ? null : institucion.Trim();
        Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        FechaDesde = fechaDesde;
        FechaHasta = fechaHasta;
    }
}
