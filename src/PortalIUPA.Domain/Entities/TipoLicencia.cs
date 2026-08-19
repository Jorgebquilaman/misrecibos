using System.ComponentModel.DataAnnotations.Schema;
using PortalIUPA.Domain.Enums;

namespace PortalIUPA.Domain.Entities;

/// <summary>Catálogo de tipos de licencia con límites mensuales/anuales y cadena de niveles de aprobación.</summary>
public sealed class TipoLicencia
{
    public Guid Id { get; private set; }
    public string Nombre { get; private set; } = null!;
    public int? LimiteMensual { get; private set; }
    public int? LimiteAnual { get; private set; }
    public string? Descripcion { get; private set; }
    public bool RequiereAdjunto { get; private set; }
    public bool Activo { get; private set; }
    public List<NivelAprobacion> Niveles { get; private set; } = new();

    private TipoLicencia() { }

    public TipoLicencia(string nombre, int? limiteMensual = null, int? limiteAnual = null, string? descripcion = null,
        bool requiereAdjunto = false)
    {
        if (string.IsNullOrWhiteSpace(nombre)) throw new ArgumentException("El nombre es obligatorio.", nameof(nombre));
        if (limiteMensual is <= 0) throw new ArgumentException("El límite mensual debe ser mayor a cero.", nameof(limiteMensual));
        if (limiteAnual is <= 0) throw new ArgumentException("El límite anual debe ser mayor a cero.", nameof(limiteAnual));

        Id = Guid.NewGuid();
        Nombre = nombre.Trim();
        LimiteMensual = limiteMensual;
        LimiteAnual = limiteAnual;
        Descripcion = descripcion;
        RequiereAdjunto = requiereAdjunto;
        Activo = true;
    }

    public void Actualizar(string nombre, int? limiteMensual, int? limiteAnual, string? descripcion, bool requiereAdjunto)
    {
        if (string.IsNullOrWhiteSpace(nombre)) throw new ArgumentException("El nombre es obligatorio.", nameof(nombre));
        if (limiteMensual is <= 0) throw new ArgumentException("El límite mensual debe ser mayor a cero.", nameof(limiteMensual));
        if (limiteAnual is <= 0) throw new ArgumentException("El límite anual debe ser mayor a cero.", nameof(limiteAnual));

        Nombre = nombre.Trim();
        LimiteMensual = limiteMensual;
        LimiteAnual = limiteAnual;
        Descripcion = descripcion;
        RequiereAdjunto = requiereAdjunto;
    }

    public void Activar() => Activo = true;

    public void Desactivar() => Activo = false;

    public void AgregarNivel(AprobadorRequerido rolRequerido)
    {
        var orden = Niveles.Count == 0 ? 1 : Niveles.Max(n => n.Orden) + 1;
        Niveles.Add(new NivelAprobacion(Id, orden, rolRequerido));
    }

    public void QuitarNivel(Guid nivelId)
    {
        var nivel = Niveles.FirstOrDefault(n => n.Id == nivelId)
            ?? throw new InvalidOperationException("El nivel de aprobación no existe.");
        Niveles.Remove(nivel);
        Reordenar();
    }

    public void Reordenar(IReadOnlyList<Guid>? ordenIds = null)
    {
        var niveles = ordenIds is null
            ? Niveles.OrderBy(n => n.Orden).ToList()
            : ordenIds.Select(id => Niveles.First(n => n.Id == id)).ToList();

        if (ordenIds is not null &&
            (niveles.Count != Niveles.Count || niveles.DistinctBy(n => n.Id).Count() != Niveles.Count))
            throw new ArgumentException("El orden debe contener exactamente los niveles existentes.", nameof(ordenIds));

        for (var i = 0; i < niveles.Count; i++)
            niveles[i].ReasignarOrden(i + 1);
    }

    [NotMapped]
    public IReadOnlyList<NivelAprobacion> NivelesOrdenados =>
        Niveles.OrderBy(n => n.Orden).ToList();

    /// <summary>Niveles ordenados; el que debe actuar primero queda al frente.</summary>
    [NotMapped]
    public NivelAprobacion? PrimerNivel => NivelesOrdenados.FirstOrDefault();
}