using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Infrastructure.External.Jasper;

public sealed class JasperOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8080/jasperserver";
    public string Usuario { get; set; } = "jasperadmin";
    public string Contrasena { get; set; } = "jasperadmin";
}

/// <summary>
/// Adaptador REST v2 hacia JasperReports Server (auth básica). Genera el PDF del recibo con
/// los parámetros del reporte legacy Mapuche (nroliq, nroleg_f, nroleg_i). Sin lógica de negocio.
/// </summary>
public sealed class JasperReportClient : IJasperReportClient
{
    private readonly HttpClient _http;
    private readonly JasperOptions _options;

    public JasperReportClient(HttpClient http, IOptions<JasperOptions> options)
    {
        _http = http;
        _options = options.Value;

        var credenciales = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.Usuario}:{_options.Contrasena}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credenciales);
    }

    public async Task<byte[]> GetPdfAsync(string reporteRuta, IReadOnlyDictionary<string, string> parametros,
        CancellationToken ct = default)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/rest_v2/reports/{reporteRuta}.pdf";

        var parametrosOrdenados = parametros.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase);
        var query = string.Join("&", parametrosOrdenados.Select(p =>
            $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
        var urlCompleta = query.Length > 0 ? $"{url}?{query}" : url;

        using var respuesta = await _http.GetAsync(urlCompleta, HttpCompletionOption.ResponseContentRead, ct);
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.Content.ReadAsByteArrayAsync(ct);
    }
}