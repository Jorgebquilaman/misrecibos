using PortalIUPA.Domain.Enums;

namespace PortalIUPA.Domain.Entities;

/// <summary>Anuncio institucional con vigencia, prioridad y alcance (reemplaza al legacy MensajeInicio).</summary>
public sealed class Anuncio
{
    public Guid Id { get; private set; }
    public string Titulo { get; private set; } = null!;
    public string Cuerpo { get; private set; } = null!;
    public DateOnly? FechaDesde { get; private set; }
    public DateOnly? FechaHasta { get; private set; }
    public PrioridadAnuncio Prioridad { get; private set; }
    public TipoAnuncio Tipo { get; private set; }
    public AlcanceAnuncio Alcance { get; private set; }
    public Guid? AreaId { get; private set; }
    public Rol? Rol { get; private set; }
    public bool Activo { get; private set; }
    public Guid CreadoPor { get; private set; }
    public DateTime FechaCreacion { get; private set; }

    private Anuncio() { }

    public Anuncio(string titulo, string cuerpo, Guid creadoPor, PrioridadAnuncio prioridad = PrioridadAnuncio.Normal,
        TipoAnuncio tipo = TipoAnuncio.Informativo, DateOnly? fechaDesde = null, DateOnly? fechaHasta = null,
        AlcanceAnuncio alcance = AlcanceAnuncio.Todos, Guid? areaId = null, Rol? rol = null)
    {
        if (string.IsNullOrWhiteSpace(titulo)) throw new ArgumentException("El título es obligatorio.", nameof(titulo));
        if (string.IsNullOrWhiteSpace(cuerpo)) throw new ArgumentException("El cuerpo es obligatorio.", nameof(cuerpo));
        if (fechaHasta is not null && fechaDesde is not null && fechaHasta < fechaDesde)
            throw new ArgumentException("La fecha de fin no puede ser anterior a la de inicio.", nameof(fechaHasta));

        Id = Guid.NewGuid();
        Titulo = titulo.Trim();
        Cuerpo = cuerpo;
        Prioridad = prioridad;
        Tipo = tipo;
        FechaDesde = fechaDesde;
        FechaHasta = fechaHasta;
        Alcance = alcance;
        AreaId = areaId;
        Rol = rol;
        Activo = true;
        CreadoPor = creadoPor;
        FechaCreacion = DateTime.UtcNow;
    }

    public bool EstaVigente(DateOnly fecha) =>
        Activo && (FechaDesde is null || fecha >= FechaDesde) && (FechaHasta is null || fecha <= FechaHasta);

    public bool AplicaA(Empleado empleado) =>
        Alcance switch
        {
            AlcanceAnuncio.Todos => true,
            AlcanceAnuncio.Area => empleado.AreaId == AreaId,
            AlcanceAnuncio.Rol => Rol.HasValue && empleado.TieneRol(Rol.Value),
            _ => false,
        };

    public void Actualizar(string titulo, string cuerpo, PrioridadAnuncio prioridad, TipoAnuncio tipo,
        DateOnly? fechaDesde, DateOnly? fechaHasta, AlcanceAnuncio alcance, Guid? areaId, Rol? rol)
    {
        if (string.IsNullOrWhiteSpace(titulo)) throw new ArgumentException("El título es obligatorio.", nameof(titulo));
        if (string.IsNullOrWhiteSpace(cuerpo)) throw new ArgumentException("El cuerpo es obligatorio.", nameof(cuerpo));
        if (fechaHasta is not null && fechaDesde is not null && fechaHasta < fechaDesde)
            throw new ArgumentException("La fecha de fin no puede ser anterior a la de inicio.", nameof(fechaHasta));

        Titulo = titulo.Trim();
        Cuerpo = cuerpo;
        Prioridad = prioridad;
        Tipo = tipo;
        FechaDesde = fechaDesde;
        FechaHasta = fechaHasta;
        Alcance = alcance;
        AreaId = areaId;
        Rol = rol;
    }

    public void Activar() => Activo = true;

    public void Desactivar() => Activo = false;
}