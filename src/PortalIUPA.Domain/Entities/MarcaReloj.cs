using PortalIUPA.Domain.Enums;

namespace PortalIUPA.Domain.Entities;

/// <summary>Marca de reloj (entrada/salida) sincronizada desde la base del checador biométrico.</summary>
public sealed class MarcaReloj
{
    public Guid Id { get; private set; }
    public Guid EmpleadoId { get; private set; }
    public DateTime FechaHora { get; private set; }
    public TipoMarca Tipo { get; private set; }
    public string? Origen { get; private set; }

    private MarcaReloj() { }

    public MarcaReloj(Guid empleadoId, DateTime fechaHora, TipoMarca tipo, string? origen = null)
    {
        Id = Guid.NewGuid();
        EmpleadoId = empleadoId;
        FechaHora = fechaHora;
        Tipo = tipo;
        Origen = origen;
    }

    public DateOnly Fecha => DateOnly.FromDateTime(FechaHora);

    /// <summary>Edición de una marca manual (fecha/hora y tipo). Solo aplica a marcas de origen "manual".</summary>
    public void Editar(DateTime fechaHora, TipoMarca tipo)
    {
        FechaHora = fechaHora;
        Tipo = tipo;
    }
}