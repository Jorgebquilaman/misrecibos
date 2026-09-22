namespace PortalIUPA.Domain.Ports;

/// <summary>Datos del CV del empleado para el generador PDF.</summary>
public sealed record DatoCvCertificado(
    string Nombre, string Institucion, string Tipo, DateOnly FechaObtencion);

/// <summary>Experiencia laboral para el CV.</summary>
public sealed record DatoCvExperiencia(
    string Puesto, string Institucion, string? Descripcion, DateOnly FechaDesde, DateOnly? FechaHasta);

/// <summary>Antecedente académico para el CV.</summary>
public sealed record DatoCvAntecedente(
    string Titulo, string Institucion, string Nivel, string? Descripcion, DateOnly FechaDesde, DateOnly? FechaHasta);

/// <summary>Ítem de antecedente profesional/artístico, producción u otro antecedente para el CV.</summary>
public sealed record DatoCvItem(
    string Seccion, string Categoria, string Titulo, string? Institucion, string? Descripcion,
    DateOnly FechaDesde, DateOnly? FechaHasta);

/// <summary>Encabezado con los datos personales que muestra el CV.</summary>
public sealed record DatosCvEmpleado(
    string ApellidoYNombre, int Legajo, string Correo, string? Documento, string? Area,
    string? Observaciones = null, string? Telefono = null);

public sealed record DatosCvPdf(
    DatosCvEmpleado Empleado,
    IReadOnlyList<DatoCvCertificado> Certificados,
    IReadOnlyList<DatoCvExperiencia> Experiencias,
    IReadOnlyList<DatoCvAntecedente> Antecedentes,
    IReadOnlyList<DatoCvItem> CvItems);

/// <summary>Archivo anexo del CV (PDF o imagen) que se adjunta al final. Los campos opcionales
/// alimentan la fila del índice de adjuntos (institución, tipo/categoría y fecha de referencia).</summary>
public sealed record ArchivoCertificadoCv(string Nombre, string ContentType, byte[] Contenido,
    string? Institucion = null, string? Tipo = null, string? Fecha = null);

public interface IGeneradorPdfCv
{
    /// <summary>Genera el CV completo: resumen + certificados del empleado como páginas adicionales.</summary>
    Task<byte[]> GenerarAsync(DatosCvPdf datos, IReadOnlyList<ArchivoCertificadoCv> archivos,
        CancellationToken ct = default);
}
