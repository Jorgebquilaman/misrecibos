namespace PortalIUPA.Domain.Entities;

/// <summary>Jerarquía de supervisión: un empleado (responsable) tiene a cargo a otros (con historial de vigencia).</summary>
public sealed class RelacionACargo
{
    public Guid Id { get; private set; }
    public Guid ResponsableId { get; private set; }
    public Guid EmpleadoId { get; private set; }
    public bool AutorizaMarcas { get; private set; }
    public DateOnly FechaDesde { get; private set; }
    public DateOnly? FechaHasta { get; private set; }

    private RelacionACargo() { }

    public RelacionACargo(Guid responsableId, Guid empleadoId, bool autorizaMarcas, DateOnly fechaDesde)
    {
        Id = Guid.NewGuid();
        ResponsableId = responsableId;
        EmpleadoId = empleadoId;
        AutorizaMarcas = autorizaMarcas;
        FechaDesde = fechaDesde;
    }

    public bool EstaVigente(DateOnly fecha) => FechaDesde <= fecha && (FechaHasta is null || fecha <= FechaHasta);

    public void Finalizar(DateOnly fecha)
    {
        if (FechaHasta is not null)
            throw new InvalidOperationException("La relación ya fue finalizada.");
        FechaHasta = fecha;
    }

    public void CambiarAutorizacionDeMarcas(bool autoriza) => AutorizaMarcas = autoriza;
}