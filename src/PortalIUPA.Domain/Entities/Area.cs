namespace PortalIUPA.Domain.Entities;

/// <summary>Unidad organizativa (dirección, secretaría, área, departamento). Forma un árbol.</summary>
public sealed class Area
{
    public Guid Id { get; private set; }
    public string Nombre { get; private set; } = null!;
    public string Codigo { get; private set; } = null!;
    public Guid? AreaPadreId { get; private set; }
    public bool Activa { get; private set; }

    private Area() { }

    public Area(string nombre, string codigo, Guid? areaPadreId = null)
    {
        if (string.IsNullOrWhiteSpace(nombre)) throw new ArgumentException("El nombre es obligatorio.", nameof(nombre));
        if (string.IsNullOrWhiteSpace(codigo)) throw new ArgumentException("El código es obligatorio.", nameof(codigo));

        Id = Guid.NewGuid();
        Nombre = nombre.Trim();
        Codigo = codigo.Trim().ToUpperInvariant();
        AreaPadreId = areaPadreId;
        Activa = true;
    }

    public void Renombrar(string nombre) => Nombre = nombre.Trim();

    public void CambiarCodigo(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) throw new ArgumentException("El código es obligatorio.", nameof(codigo));
        Codigo = codigo.Trim().ToUpperInvariant();
    }

    public void ReasignarPadre(Guid? areaPadreId) => AreaPadreId = areaPadreId;

    public void Activar() => Activa = true;

    public void Desactivar() => Activa = false;
}