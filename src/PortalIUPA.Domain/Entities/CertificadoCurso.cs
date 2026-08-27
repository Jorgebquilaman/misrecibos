using PortalIUPA.Domain.ValueObjects;

namespace PortalIUPA.Domain.Entities;

public enum TipoEstudio
{
    Curso = 1,
    Taller = 2,
    Diplomatura = 3,
    Carrera = 4,
    Posgrado = 5,
    Otro = 6
}

/// <summary>Estado de revisión de un certificado de CV por parte de RRHH/Administración.</summary>
public enum EstadoCertificadoCv
{
    Pendiente = 0,
    Verificado = 1,
    Observado = 2
}

/// <summary>Certificado de curso/carrera subido por el empleado para mantener su CV actualizado.</summary>
public sealed class CertificadoCurso
{
    public Guid Id { get; private set; }
    public Guid EmpleadoId { get; private set; }
    public string Nombre { get; private set; } = null!;
    public string Institucion { get; private set; } = null!;
    public TipoEstudio Tipo { get; private set; }
    public DateOnly FechaObtencion { get; private set; }
    public Guid AdjuntoId { get; private set; }
    public EstadoCertificadoCv Estado { get; private set; }
    public string? ComentarioRevision { get; private set; }
    public DateTime FechaCarga { get; private set; }

    private CertificadoCurso() { }

    public CertificadoCurso(Guid empleadoId, string nombre, string institucion, TipoEstudio tipo,
        DateOnly fechaObtencion, Guid adjuntoId)
    {
        if (string.IsNullOrWhiteSpace(nombre)) throw new ArgumentException("El nombre es obligatorio.", nameof(nombre));
        if (string.IsNullOrWhiteSpace(institucion)) throw new ArgumentException("La institución es obligatoria.", nameof(institucion));

        Id = Guid.NewGuid();
        EmpleadoId = empleadoId;
        Nombre = nombre.Trim();
        Institucion = institucion.Trim();
        Tipo = tipo;
        FechaObtencion = fechaObtencion;
        AdjuntoId = adjuntoId;
        Estado = EstadoCertificadoCv.Pendiente;
        FechaCarga = DateTime.UtcNow;
    }

    /// <summary>Revisión de RRHH: verificado (válido) u observado (con comentario).</summary>
    public void Revisar(bool verificado, string? comentario)
    {
        Estado = verificado ? EstadoCertificadoCv.Verificado : EstadoCertificadoCv.Observado;
        ComentarioRevision = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim();
    }
}
