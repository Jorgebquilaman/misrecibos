namespace PortalIUPA.Domain.Entities;

/// <summary>Período de liquidación de haberes (ej. "2025-06"). Los activos habilitan la descarga de recibos.</summary>
public sealed class Periodo
{
    public Guid Id { get; private set; }
    public string Codigo { get; private set; } = null!;
    public string? Descripcion { get; private set; }
    public bool Activo { get; private set; }

    /// <summary>ID de la liquidación en el sistema legacy (columna Periodos.Periodo de SQL Server). Es el parámetro <c>nroliq</c> del reporte de recibo en Jasper.</summary>
    public int? NroLiq { get; private set; }

    private Periodo() { }

    public Periodo(string codigo, string? descripcion = null, int? nroLiq = null)
    {
        if (string.IsNullOrWhiteSpace(codigo)) throw new ArgumentException("El código es obligatorio.", nameof(codigo));

        Id = Guid.NewGuid();
        Codigo = codigo.Trim();
        Descripcion = descripcion;
        NroLiq = nroLiq;
        Activo = false;
    }

    public void Activar() => Activo = true;

    public void Desactivar() => Activo = false;

    public void Actualizar(string? descripcion, int? nroLiq) 
    { 
        Descripcion = descripcion;
        NroLiq = nroLiq;
    }
}