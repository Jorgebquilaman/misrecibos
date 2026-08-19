using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.ValueObjects;

namespace PortalIUPA.Domain.Entities;

public sealed class Empleado
{
    public Guid Id { get; private set; }
    public int Legajo { get; private set; }
    public string Nombre { get; private set; } = null!;
    public string Apellido { get; private set; } = null!;
    public string? Dni { get; private set; }
    public string? Cuil { get; private set; }
    public Email Correo { get; private set; } = null!;
    public Guid? AreaId { get; private set; }
    public bool Activo { get; private set; }
    public List<Rol> Roles { get; private set; } = new();

    private Empleado() { }

    public Empleado(int legajo, string nombre, string apellido, Email correo, string? dni = null, string? cuil = null)
    {
        if (legajo <= 0) throw new ArgumentException("El legajo debe ser mayor a cero.", nameof(legajo));
        if (string.IsNullOrWhiteSpace(nombre)) throw new ArgumentException("El nombre es obligatorio.", nameof(nombre));
        if (string.IsNullOrWhiteSpace(apellido)) throw new ArgumentException("El apellido es obligatorio.", nameof(apellido));

        Id = Guid.NewGuid();
        Legajo = legajo;
        Nombre = nombre.Trim();
        Apellido = apellido.Trim();
        Dni = dni;
        Cuil = cuil;
        Correo = correo;
        Activo = true;
        Roles.Add(Rol.Empleado);
    }

    public string NombreCompleto => $"{Apellido}, {Nombre}";

    public bool TieneRol(Rol rol) => Roles.Contains(rol);

    public bool EsAdministrador => TieneRol(Rol.Administrador);

    public void AgregarRol(Rol rol)
    {
        if (!Roles.Contains(rol)) Roles.Add(rol);
    }

    public void QuitarRol(Rol rol)
    {
        if (rol == Rol.Empleado) throw new InvalidOperationException("El rol base 'Empleado' no puede quitarse.");
        Roles.Remove(rol);
    }

    public void AsignarArea(Guid? areaId) => AreaId = areaId;

    public void ActualizarDatos(string nombre, string apellido, string? dni, string? cuil)
    {
        if (string.IsNullOrWhiteSpace(nombre)) throw new ArgumentException("El nombre es obligatorio.", nameof(nombre));
        if (string.IsNullOrWhiteSpace(apellido)) throw new ArgumentException("El apellido es obligatorio.", nameof(apellido));

        Nombre = nombre.Trim();
        Apellido = apellido.Trim();
        Dni = dni;
        Cuil = cuil;
    }

    public void Activar() => Activo = true;

    public void Desactivar() => Activo = false;
}