namespace PortalIUPA.Domain.Entities;

/// <summary>
/// Dashboard visual: una consulta base + controles (KPIs, KPOs, gráficos, tablas)
/// definidos como JSON, con filtros globales que se aplican a todos los controles.
/// </summary>
public sealed class DashboardDefinicion
{
    public Guid Id { get; private set; }
    public string Nombre { get; private set; } = default!;
    public string? Descripcion { get; private set; }
    public string QuerySql { get; private set; } = default!;
    public string DefinicionJson { get; private set; } = "{}";
    public string Conexion { get; private set; } = ReporteDefinicion.ConexionPrincipal;
    public string CreadoPorEmail { get; private set; } = default!;
    public bool Activo { get; private set; }
    public DateTime CreadoEn { get; private set; }
    public DateTime ActualizadoEn { get; private set; }

    private DashboardDefinicion() { }

    public DashboardDefinicion(string nombre, string querySql, string definicionJson, string creadoPorEmail, string? descripcion, string? conexion)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre del dashboard es obligatorio.");
        if (string.IsNullOrWhiteSpace(querySql))
            throw new ArgumentException("La consulta SQL del dashboard es obligatoria.");
        if (string.IsNullOrWhiteSpace(creadoPorEmail))
            throw new ArgumentException("El creador del dashboard es obligatorio.");

        Id = Guid.NewGuid();
        Nombre = nombre.Trim();
        Descripcion = descripcion?.Trim();
        QuerySql = querySql.Trim();
        DefinicionJson = string.IsNullOrWhiteSpace(definicionJson) ? "{}" : definicionJson;
        Conexion = string.IsNullOrWhiteSpace(conexion) ? ReporteDefinicion.ConexionPrincipal : conexion.Trim();
        CreadoPorEmail = creadoPorEmail.Trim().ToLowerInvariant();
        Activo = true;
        CreadoEn = DateTime.UtcNow;
        ActualizadoEn = CreadoEn;
    }

    public void Editar(string nombre, string? descripcion, string querySql, string definicionJson, string? conexion = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre del dashboard es obligatorio.");
        if (string.IsNullOrWhiteSpace(querySql))
            throw new ArgumentException("La consulta SQL del dashboard es obligatoria.");

        Nombre = nombre.Trim();
        Descripcion = descripcion?.Trim();
        QuerySql = querySql.Trim();
        DefinicionJson = string.IsNullOrWhiteSpace(definicionJson) ? "{}" : definicionJson;
        if (!string.IsNullOrWhiteSpace(conexion)) Conexion = conexion.Trim();
        ActualizadoEn = DateTime.UtcNow;
    }

    public void Desactivar() => Activo = false;
    public void Reactivar() => Activo = true;

    public bool PuedeEditar(string email, IReadOnlyCollection<string> roles) =>
        roles.Contains("Administrador", StringComparer.OrdinalIgnoreCase) ||
        string.Equals(CreadoPorEmail, email, StringComparison.OrdinalIgnoreCase);
}
