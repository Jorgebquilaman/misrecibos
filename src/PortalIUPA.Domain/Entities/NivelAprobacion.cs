using PortalIUPA.Domain.Enums;

namespace PortalIUPA.Domain.Entities;

/// <summary>Escalón de la cadena de aprobación de un tipo de licencia (orden + rol/puesto requerido).</summary>
public sealed class NivelAprobacion
{
    public Guid Id { get; private set; }
    public Guid TipoLicenciaId { get; private set; }
    public int Orden { get; private set; }
    public AprobadorRequerido RolRequerido { get; private set; }

    private NivelAprobacion() { }

    public NivelAprobacion(Guid tipoLicenciaId, int orden, AprobadorRequerido rolRequerido)
    {
        if (orden <= 0) throw new ArgumentException("El orden debe ser mayor a cero.", nameof(orden));

        Id = Guid.NewGuid();
        TipoLicenciaId = tipoLicenciaId;
        Orden = orden;
        RolRequerido = rolRequerido;
    }

    public void ReasignarOrden(int orden)
    {
        if (orden <= 0) throw new ArgumentException("El orden debe ser mayor a cero.", nameof(orden));
        Orden = orden;
    }
}