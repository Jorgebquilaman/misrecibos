using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PortalIUPA.Infrastructure.Pdf.Trazabilidad;

/// <summary>
/// Acceso de lectura al registro de trazabilidad (trace-registry.json).
/// </summary>
public sealed class TrazabilidadRegistroRepository
{
    private static readonly JsonSerializerOptions JsonOpts = new();

    private readonly string _ruta;

    public TrazabilidadRegistroRepository(IOptions<TrazabilidadOptions> options)
        => _ruta = Path.GetFullPath(options.Value.RutaRegistro);

    public string RutaRegistro => _ruta;

    public bool ExisteRegistro() => File.Exists(_ruta);

    public async Task<List<TrazabilidadRegistro>> ObtenerTodosAsync(CancellationToken ct = default)
        => await LeerAsync(ct);

    public async Task<TrazabilidadRegistro?> ObtenerPorTraceIdAsync(string traceId, CancellationToken ct = default)
    {
        var todos = await LeerAsync(ct);
        return todos.FirstOrDefault(r =>
            string.Equals(r.TraceId, Normalizar(traceId), StringComparison.OrdinalIgnoreCase));
    }

    public async Task<TrazabilidadRegistro?> BuscarPorArchivoAsync(string archivo, string? sha256 = null, CancellationToken ct = default)
    {
        var todos = await LeerAsync(ct);
        if (todos.Count == 0) return null;

        var nombre = NormalizarNombreArchivo(Path.GetFileName(archivo));
        // Coincidencia primaria por nombre normalizado (InputFile u OutputFile).
        // Al subir el PDF ya marcado, su SHA-256 difiere del hash del PDF de entrada,
        // por lo que el hash se usa como refinamiento, nunca como requisito del match por nombre.
        var porNombre = todos.Where(r =>
                string.Equals(NormalizarNombreArchivo(r.InputFile), nombre, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(NormalizarNombreArchivo(r.OutputFile), nombre, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (porNombre.Count > 0)
        {
            if (sha256 is not null)
            {
                var porHash = porNombre.FirstOrDefault(r =>
                    string.Equals(r.InputHashSha256, sha256, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r.OutputHashSha256, sha256, StringComparison.OrdinalIgnoreCase));
                if (porHash is not null) return porHash;
            }
            return porNombre.OrderByDescending(r => r.FechaHora).First();
        }

        // Fallback: solo por hash de entrada o salida (p. ej. archivo renombrado por el navegador)
        if (sha256 is not null)
        {
            return todos.FirstOrDefault(r =>
                string.Equals(r.InputHashSha256, sha256, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.OutputHashSha256, sha256, StringComparison.OrdinalIgnoreCase));
        }
        return null;
    }

    /// <summary>
    /// Normaliza nombres para comparación: quita sufijos de duplicado del navegador (" (1)", " (2)"...)
    /// y la variante "-trazabilidad", de modo que input, output y descargas renombradas crucen entre sí.
    /// </summary>
    private static string NormalizarNombreArchivo(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return string.Empty;
        var sinExtension = Path.GetFileNameWithoutExtension(nombre);
        var extension = Path.GetExtension(nombre);
        var normalizado = System.Text.RegularExpressions.Regex.Replace(sinExtension, @"\s*\(\d+\)$", string.Empty);
        if (normalizado.EndsWith("-trazabilidad", StringComparison.OrdinalIgnoreCase))
            normalizado = normalizado[..^"-trazabilidad".Length];
        return normalizado + extension;
    }

    private async Task<List<TrazabilidadRegistro>> LeerAsync(CancellationToken ct)
    {
        if (!File.Exists(_ruta)) return new List<TrazabilidadRegistro>();
        try
        {
            var json = await File.ReadAllTextAsync(_ruta, ct);
            return JsonSerializer.Deserialize<List<TrazabilidadRegistro>>(json, JsonOpts)
                   ?? new List<TrazabilidadRegistro>();
        }
        catch
        {
            return new List<TrazabilidadRegistro>();
        }
    }

    private static string Normalizar(string traceId)
    {
        var t = traceId.Trim().ToUpperInvariant();
        return t.StartsWith("DOC-") ? t : $"DOC-{t}";
    }
}
