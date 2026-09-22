namespace PortalIUPA.Domain.Entities;

/// <summary>
/// Reporte visual diseñado con el builder: se persiste la consulta SQL base
/// y la definición completa (filas, valores, filtros, gráficos y subreportes) como JSON.
/// </summary>
public sealed class ReporteDefinicion
{
    public const string ConexionPrincipal = "PortalIUPA";
    public Guid Id { get; private set; }
    public string Nombre { get; private set; } = default!;
    public string? Descripcion { get; private set; }
    public string QuerySql { get; private set; } = default!;
    public string DefinicionJson { get; private set; } = "{}";
    /// <summary>Diseño de apariencia (canvas): títulos, logos, imágenes, código de barras, posiciones.</summary>
    public string DisenoJson { get; private set; } = "{}";
    public string Conexion { get; private set; } = ConexionPrincipal;
    public string CreadoPorEmail { get; private set; } = default!;
    public bool Activo { get; private set; }
    public DateTime CreadoEn { get; private set; }
    public DateTime ActualizadoEn { get; private set; }

    private readonly List<ReportePermiso> _permisos = new();
    public IReadOnlyList<ReportePermiso> Permisos => _permisos;

    private ReporteDefinicion() { }

    public ReporteDefinicion(string nombre, string querySql, string definicionJson, string creadoPorEmail, string? descripcion, string? conexion = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre del reporte es obligatorio.");
        if (string.IsNullOrWhiteSpace(querySql))
            throw new ArgumentException("La consulta SQL del reporte es obligatoria.");
        if (string.IsNullOrWhiteSpace(creadoPorEmail))
            throw new ArgumentException("El creador del reporte es obligatorio.");

        Id = Guid.NewGuid();
        Nombre = nombre.Trim();
        Descripcion = descripcion?.Trim();
        QuerySql = querySql.Trim();
        DefinicionJson = string.IsNullOrWhiteSpace(definicionJson) ? "{}" : definicionJson;
        Conexion = string.IsNullOrWhiteSpace(conexion) ? ConexionPrincipal : conexion.Trim();
        CreadoPorEmail = creadoPorEmail.Trim().ToLowerInvariant();
        Activo = true;
        CreadoEn = DateTime.UtcNow;
        ActualizadoEn = CreadoEn;
    }

    public void Editar(string nombre, string? descripcion, string querySql, string definicionJson, string? conexion = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre del reporte es obligatorio.");
        if (string.IsNullOrWhiteSpace(querySql))
            throw new ArgumentException("La consulta SQL del reporte es obligatoria.");

        Nombre = nombre.Trim();
        Descripcion = descripcion?.Trim();
        QuerySql = querySql.Trim();
        DefinicionJson = string.IsNullOrWhiteSpace(definicionJson) ? "{}" : definicionJson;
        if (!string.IsNullOrWhiteSpace(conexion)) Conexion = conexion.Trim();
        ActualizadoEn = DateTime.UtcNow;
    }

    public void EstablecerDiseno(string disenoJson)
    {
        DisenoJson = string.IsNullOrWhiteSpace(disenoJson) ? "{}" : disenoJson;
        ActualizadoEn = DateTime.UtcNow;
    }

    public void Desactivar()
    {
        Activo = false;
        ActualizadoEn = DateTime.UtcNow;
    }

    public void Reactivar()
    {
        Activo = true;
        ActualizadoEn = DateTime.UtcNow;
    }

    public void ReemplazarPermisos(IEnumerable<(string? Email, string? Rol)> permisos)
    {
        _permisos.Clear();
        foreach (var (email, rol) in permisos)
        {
            if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(rol))
                continue;
            _permisos.Add(new ReportePermiso(Id, email?.Trim().ToLowerInvariant(), rol?.Trim()));
        }
    }

    /// <summary>
    /// Un usuario puede ver el reporte si es el creador, o si su correo/algún rol figura en los permisos.
    /// </summary>
    public bool EsVisiblePara(string email, IReadOnlyCollection<string> roles)
    {
        if (string.Equals(CreadoPorEmail, email, StringComparison.OrdinalIgnoreCase))
            return true;
        return Permisos.Any(p =>
            (!string.IsNullOrEmpty(p.Email) && string.Equals(p.Email, email, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(p.Rol) && roles.Contains(p.Rol, StringComparer.OrdinalIgnoreCase)));
    }
}
