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

/// <summary>Encabezado con los datos personales que muestra el CV.</summary>
public sealed record DatosCvEmpleado(
    string ApellidoYNombre, int Legajo, string Correo, string? Documento, string? Area,
    string? Observaciones = null, string? Telefono = null);

public sealed record DatosCvPdf(
    DatosCvEmpleado Empleado,
    IReadOnlyList<DatoCvCertificado> Certificados,
    IReadOnlyList<DatoCvExperiencia> Experiencias,
    IReadOnlyList<DatoCvAntecedente> Antecedentes);

/// <summary>Archivo de un certificado (PDF o imagen) que se adjunta al final del CV.</summary>
public sealed record ArchivoCertificadoCv(string Nombre, string ContentType, byte[] Contenido);

public interface IGeneradorPdfCv
{
    /// <summary>Genera el CV completo: resumen + certificados del empleado como páginas adicionales.</summary>
    Task<byte[]> GenerarAsync(DatosCvPdf datos, IReadOnlyList<ArchivoCertificadoCv> archivos,
        CancellationToken ct = default);
}
