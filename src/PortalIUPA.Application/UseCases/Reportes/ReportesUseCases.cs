using System.Text.Json;
using System.Text.Json.Serialization;
using MediatR;
using PortalIUPA.Application.Common;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Application.UseCases.Reportes;

// ---------------- Definición (crear / actualizar) ----------------

public sealed record GuardarReporteCommand(
    Guid? Id,
    string Nombre,
    string? Descripcion,
    string QuerySql,
    string? Conexion,
    ReporteDefinicionDto Definicion,
    string UsuarioEmail) : IRequest<Guid>;

public sealed class GuardarReporteCommandHandler : IRequestHandler<GuardarReporteCommand, Guid>
{
    private readonly IReporteRepository _repo;
    private readonly IMotorReportes _motor;

    public GuardarReporteCommandHandler(IReporteRepository repo, IMotorReportes motor)
    {
        _repo = repo;
        _motor = motor;
    }

    public async Task<Guid> Handle(GuardarReporteCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.UsuarioEmail))
            throw new AccesoDenegadoException("No se pudo identificar el usuario.");

        // La conexión debe estar configurada en el servidor.
        if (!string.IsNullOrWhiteSpace(request.Conexion) &&
            !_motor.ConexionesDisponibles.Contains(request.Conexion, StringComparer.OrdinalIgnoreCase))
            throw new ReglaDeNegocioException($"La conexión '{request.Conexion}' no está configurada en el servidor.");

        // Validamos la consulta (solo lectura), sus columnas, y que cada campo usado en el diseño exista en la consulta.
        var columnas = await _motor.ObtenerMetadataAsync(request.QuerySql, request.Conexion, ct);
        var nombres = columnas.Select(c => c.Nombre).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var referenciados = request.Definicion.Filas.Select(f => f.Campo)
            .Concat(request.Definicion.Valores.Select(v => v.Campo))
            .Concat(request.Definicion.Columnas.Select(c => c.Campo))
            .Concat(request.Definicion.Filtros.Select(f => f.Campo))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .Where(c => !nombres.Contains(c))
            .ToList();
        if (referenciados.Count > 0)
            throw new ReglaDeNegocioException(
                $"El diseño usa columnas que no están en la consulta: {string.Join(", ", referenciados)}. " +
                $"Columnas disponibles: {string.Join(", ", columnas.Select(c => c.Nombre))}.");

        var json = JsonSerializer.Serialize(request.Definicion, ReportesJson.Opciones);

        if (request.Id is null)
        {
            var reporte = new PortalIUPA.Domain.Entities.ReporteDefinicion(
                request.Nombre, request.QuerySql, json, request.UsuarioEmail, request.Descripcion, request.Conexion);
            // Por defecto todo reporte nuevo es visible para todos los usuarios (rol Empleado);
            // el creador puede restringirlo después desde el modal de permisos.
            reporte.ReemplazarPermisos([(null, "Empleado")]);
            await _repo.AddAsync(reporte, ct);
            return reporte.Id;
        }

        var existente = await _repo.GetByIdAsync(request.Id.Value, ct)
                        ?? throw new EntidadNoEncontradaException("Reporte no encontrado.");
        existente.Editar(request.Nombre, request.Descripcion, request.QuerySql, json, request.Conexion);
        await _repo.UpdateAsync(existente, ct);
        return existente.Id;
    }
}

// ---------------- Vista previa de SQL en borrador (solo Administrador) ----------------

public sealed record VistaPreviaSqlQuery(
    string QuerySql,
    string? Conexion,
    string Email,
    IReadOnlyList<string> Roles) : IRequest<ResultadoReporteDto>;

public sealed class VistaPreviaSqlQueryHandler : IRequestHandler<VistaPreviaSqlQuery, ResultadoReporteDto>
{
    private readonly IMotorReportes _motor;
    public VistaPreviaSqlQueryHandler(IMotorReportes motor) => _motor = motor;

    public async Task<ResultadoReporteDto> Handle(VistaPreviaSqlQuery request, CancellationToken ct) =>
        await _motor.EjecutarAsync(request.QuerySql, Array.Empty<CampoFilasDef>(),
            Array.Empty<CampoValorDef>(), Array.Empty<CampoColumnaDef>(),
            Array.Empty<FiltroEjecutado>(), null, 100, request.Conexion, ct);
}

// ---------------- Exploración de esquema (asistente visual) ----------------

public sealed record TablasConexionQuery(string? Conexion, string Email, IReadOnlyList<string> Roles)
    : IRequest<IReadOnlyList<TablaListaDto>>;

public sealed class TablasConexionQueryHandler : IRequestHandler<TablasConexionQuery, IReadOnlyList<TablaListaDto>>
{
    private readonly IMotorReportes _motor;
    public TablasConexionQueryHandler(IMotorReportes motor) => _motor = motor;

    public Task<IReadOnlyList<TablaListaDto>> Handle(TablasConexionQuery request, CancellationToken ct) =>
        _motor.ObtenerTablasAsync(request.Conexion, ct);
}

public sealed record TablaDetalleQuery(string Conexion, string Esquema, string Tabla, string Email, IReadOnlyList<string> Roles)
    : IRequest<TablaDetalleDto>;

public sealed class TablaDetalleQueryHandler : IRequestHandler<TablaDetalleQuery, TablaDetalleDto>
{
    private readonly IMotorReportes _motor;
    public TablaDetalleQueryHandler(IMotorReportes motor) => _motor = motor;

    public Task<TablaDetalleDto> Handle(TablaDetalleQuery request, CancellationToken ct) =>
        _motor.ObtenerTablaAsync(request.Esquema, request.Tabla, request.Conexion, ct);
}

// ---------------- Metadata de SQL en borrador (campos disponibles sin guardar) ----------------

public sealed record MetadataSqlQuery(string QuerySql, string? Conexion, string Email, IReadOnlyList<string> Roles)
    : IRequest<IReadOnlyList<ColumnaMetadata>>;

public sealed class MetadataSqlQueryHandler : IRequestHandler<MetadataSqlQuery, IReadOnlyList<ColumnaMetadata>>
{
    private readonly IMotorReportes _motor;
    public MetadataSqlQueryHandler(IMotorReportes motor) => _motor = motor;

    public Task<IReadOnlyList<ColumnaMetadata>> Handle(MetadataSqlQuery request, CancellationToken ct) =>
        _motor.ObtenerMetadataAsync(request.QuerySql, request.Conexion, ct);
}

// ---------------- Eliminar ----------------

public sealed record EliminarReporteCommand(Guid Id, string UsuarioEmail) : IRequest;

public sealed class EliminarReporteCommandHandler : IRequestHandler<EliminarReporteCommand>
{
    private readonly IReporteRepository _repo;
    public EliminarReporteCommandHandler(IReporteRepository repo) => _repo = repo;

    public async Task Handle(EliminarReporteCommand request, CancellationToken ct)
    {
        var reporte = await _repo.GetByIdAsync(request.Id, ct)
                      ?? throw new EntidadNoEncontradaException("Reporte no encontrado.");
        await _repo.DeleteAsync(reporte, ct);
    }
}

// ---------------- Listado (según permisos) ----------------

public sealed record ReporteResumenDto(
    Guid Id, string Nombre, string? Descripcion, bool Activo, string CreadoPorEmail,
    bool PuedeEditar, DateTime CreadoEn);

public sealed record ListarReportesQuery(string Email, IReadOnlyList<string> Roles)
    : IRequest<IReadOnlyList<ReporteResumenDto>>;

public sealed class ListarReportesQueryHandler : IRequestHandler<ListarReportesQuery, IReadOnlyList<ReporteResumenDto>>
{
    private readonly IReporteRepository _repo;
    public ListarReportesQueryHandler(IReporteRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<ReporteResumenDto>> Handle(ListarReportesQuery request, CancellationToken ct)
    {
        var esAdmin = request.Roles.Contains("Administrador", StringComparer.OrdinalIgnoreCase);
        var todos = await _repo.GetAllAsync(ct);
        return todos
            .Where(r => (esAdmin || r.Activo) && r.EsVisiblePara(request.Email, request.Roles))
            .Select(r => new ReporteResumenDto(
                r.Id, r.Nombre, r.Descripcion, r.Activo, r.CreadoPorEmail,
                esAdmin || string.Equals(r.CreadoPorEmail, request.Email, StringComparison.OrdinalIgnoreCase),
                r.CreadoEn))
            .ToList();
    }
}

// ---------------- Detalle de definición ----------------

public sealed record ReporteCompletoDto(
    Guid Id, string Nombre, string? Descripcion, string QuerySql, bool Activo,
    string CreadoPorEmail, bool PuedeEditar, string Conexion, ReporteDefinicionDto Definicion,
    string DisenoJson, IReadOnlyList<PermisoDto> Permisos);

public sealed record PermisoDto(string? Email, string? Rol);

public sealed record ObtenerReporteQuery(Guid Id, string Email, IReadOnlyList<string> Roles)
    : IRequest<ReporteCompletoDto>;

public sealed class ObtenerReporteQueryHandler : IRequestHandler<ObtenerReporteQuery, ReporteCompletoDto>
{
    private readonly IReporteRepository _repo;
    public ObtenerReporteQueryHandler(IReporteRepository repo) => _repo = repo;

    public async Task<ReporteCompletoDto> Handle(ObtenerReporteQuery request, CancellationToken ct)
    {
        var reporte = await _repo.GetByIdAsync(request.Id, ct)
                      ?? throw new EntidadNoEncontradaException("Reporte no encontrado.");

        var esAdmin = request.Roles.Contains("Administrador", StringComparer.OrdinalIgnoreCase);
        if (!esAdmin && !reporte.EsVisiblePara(request.Email, request.Roles))
            throw new AccesoDenegadoException("No tenés permiso para ver este reporte.");

        var definicion = Deserializar(reporte.DefinicionJson);
        return new ReporteCompletoDto(
            reporte.Id, reporte.Nombre, reporte.Descripcion, reporte.QuerySql, reporte.Activo,
            reporte.CreadoPorEmail,
            esAdmin || string.Equals(reporte.CreadoPorEmail, request.Email, StringComparison.OrdinalIgnoreCase),
            reporte.Conexion,
            definicion,
            reporte.DisenoJson,
            reporte.Permisos.Select(p => new PermisoDto(p.Email, p.Rol)).ToList());
    }

    internal static ReporteDefinicionDto Deserializar(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ReporteDefinicionDto>(json, ReportesJson.Opciones) ?? ReporteDefinicionDto.Vacio();
        }
        catch (JsonException)
        {
            return ReporteDefinicionDto.Vacio();
        }
    }
}

// ---------------- Metadata de la query ----------------

public sealed record MetadataReporteQuery(Guid Id, string Email, IReadOnlyList<string> Roles)
    : IRequest<IReadOnlyList<ColumnaMetadata>>;

public sealed class MetadataReporteQueryHandler : IRequestHandler<MetadataReporteQuery, IReadOnlyList<ColumnaMetadata>>
{
    private readonly IReporteRepository _repo;
    private readonly IMotorReportes _motor;

    public MetadataReporteQueryHandler(IReporteRepository repo, IMotorReportes motor)
    {
        _repo = repo;
        _motor = motor;
    }

    public async Task<IReadOnlyList<ColumnaMetadata>> Handle(MetadataReporteQuery request, CancellationToken ct)
    {
        var reporte = await _repo.GetByIdAsync(request.Id, ct)
                      ?? throw new EntidadNoEncontradaException("Reporte no encontrado.");
        var esAdmin = request.Roles.Contains("Administrador", StringComparer.OrdinalIgnoreCase);
        if (!esAdmin && !reporte.EsVisiblePara(request.Email, request.Roles))
            throw new AccesoDenegadoException("No tenés permiso para este reporte.");
        return await _motor.ObtenerMetadataAsync(reporte.QuerySql, reporte.Conexion, ct);
    }
}

// ---------------- Ejecución ----------------

public sealed record EjecutarReporteQuery(
    Guid Id,
    Dictionary<string, JsonElement>? Valores,
    string Email,
    IReadOnlyList<string> Roles) : IRequest<ResultadoReporteDto>;

public sealed class EjecutarReporteQueryHandler : IRequestHandler<EjecutarReporteQuery, ResultadoReporteDto>
{
    private readonly IReporteRepository _repo;
    private readonly IMotorReportes _motor;

    public EjecutarReporteQueryHandler(IReporteRepository repo, IMotorReportes motor)
    {
        _repo = repo;
        _motor = motor;
    }

    public async Task<ResultadoReporteDto> Handle(EjecutarReporteQuery request, CancellationToken ct)
    {
        var reporte = await _repo.GetByIdAsync(request.Id, ct)
                      ?? throw new EntidadNoEncontradaException("Reporte no encontrado.");
        var esAdmin = request.Roles.Contains("Administrador", StringComparer.OrdinalIgnoreCase);
        if (!esAdmin && !reporte.EsVisiblePara(request.Email, request.Roles))
            throw new AccesoDenegadoException("No tenés permiso para ejecutar este reporte.");

        var definicion = ObtenerReporteQueryHandler.Deserializar(reporte.DefinicionJson);
        var filtros = ResolverFiltros(definicion.Filtros, request.Valores);
        return await _motor.EjecutarAsync(reporte.QuerySql, definicion.Filas, definicion.Valores,
            definicion.Columnas, filtros, definicion.Orden, definicion.Limite, reporte.Conexion, ct);
    }

    internal static IReadOnlyList<FiltroEjecutado> ResolverFiltros(
        IReadOnlyList<FiltroDef> definidos, Dictionary<string, JsonElement>? valores)
    {
        var resultado = new List<FiltroEjecutado>();
        foreach (var f in definidos)
        {
            object? valor = null, valor2 = null;
            List<object?>? lista = null;

            if (f.Parametrizable && valores is not null &&
                valores.TryGetValue(f.Campo, out var elemento))
            {
                switch (f.Operador)
                {
                    case OperadorFiltro.Entre:
                        valor = Convertir(elemento, f.TipoDato);
                        if (valores.TryGetValue(f.Campo + "_hasta", out var hasta))
                            valor2 = Convertir(hasta, f.TipoDato);
                        break;
                    case OperadorFiltro.En:
                        lista = elemento.EnumerateArray()
                            .Select(e => Convertir(e, f.TipoDato))
                            .ToList();
                        break;
                    default:
                        valor = Convertir(elemento, f.TipoDato);
                        break;
                }
            }
            else if (!f.Parametrizable)
            {
                valor = ConvertirTexto(f.Valor, f.TipoDato);
                valor2 = ConvertirTexto(f.Valor2, f.TipoDato);
                lista = f.Valores is { Count: > 0 }
                    ? f.Valores.Select(v => ConvertirTexto(v, f.TipoDato)).ToList()
                    : null;
            }

            var sinValor = valor is null && valor2 is null && lista is null &&
                           f.Operador is not (OperadorFiltro.Vacio or OperadorFiltro.NoVacio);
            if (sinValor) continue; // Filtro sin valor provisto: no se aplica.

            var operadorEfectivo = f.Operador == OperadorFiltro.En && lista is not null
                ? OperadorFiltro.En
                : f.Operador;
            resultado.Add(new FiltroEjecutado(f.Campo, f.TipoDato, operadorEfectivo, valor, valor2, lista));
        }
        return resultado;
    }

    internal static object? Convertir(JsonElement elemento, TipoDatoReporte tipo) => tipo switch
    {
        TipoDatoReporte.Numero => elemento.ValueKind == JsonValueKind.Number
            ? elemento.GetDecimal()
            : decimal.TryParse(elemento.ToString(), System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : null,
        TipoDatoReporte.Booleano => elemento.ValueKind == JsonValueKind.True || elemento.ValueKind == JsonValueKind.False
            ? elemento.GetBoolean()
            : bool.TryParse(elemento.ToString(), out var b) && b,
        TipoDatoReporte.Fecha => DateTime.TryParse(elemento.ToString(), System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var f) ? f : null,
        _ => elemento.ToString()
    };

    internal static object? ConvertirTexto(string? texto, TipoDatoReporte tipo)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        return tipo switch
        {
            TipoDatoReporte.Numero => decimal.TryParse(texto, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : null,
            TipoDatoReporte.Booleano => bool.TryParse(texto, out var b) && b,
            TipoDatoReporte.Fecha => DateTime.TryParse(texto, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var f) ? f : null,
            _ => texto
        };
    }
}

// ---------------- Detalle de un grupo (al expandir la flecha) ----------------

public sealed record DetalleReporteQuery(
    Guid Id,
    Dictionary<string, JsonElement> ValoresFila,
    string Email,
    IReadOnlyList<string> Roles) : IRequest<ResultadoReporteDto>;

public sealed class DetalleReporteQueryHandler : IRequestHandler<DetalleReporteQuery, ResultadoReporteDto>
{
    private readonly IReporteRepository _repo;
    private readonly IMotorReportes _motor;

    public DetalleReporteQueryHandler(IReporteRepository repo, IMotorReportes motor)
    {
        _repo = repo;
        _motor = motor;
    }

    public async Task<ResultadoReporteDto> Handle(DetalleReporteQuery request, CancellationToken ct)
    {
        var reporte = await _repo.GetByIdAsync(request.Id, ct)
                      ?? throw new EntidadNoEncontradaException("Reporte no encontrado.");
        var esAdmin = request.Roles.Contains("Administrador", StringComparer.OrdinalIgnoreCase);
        if (!esAdmin && !reporte.EsVisiblePara(request.Email, request.Roles))
            throw new AccesoDenegadoException("No tenés permiso para ejecutar este reporte.");

        var definicion = ObtenerReporteQueryHandler.Deserializar(reporte.DefinicionJson);
        if (definicion.Filas.Count == 0)
            throw new ReglaDeNegocioException("El reporte no tiene agrupamientos, no hay detalle que mostrar.");

        // Tipos de cada campo según la metadata de la query base, para filtrar sin castear a texto.
        var tipos = await _motor.ObtenerMetadataAsync(reporte.QuerySql, reporte.Conexion, ct);
        var filtroPorTipo = new Dictionary<string, TipoDatoReporte>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tipos) filtroPorTipo[t.Nombre] = t.Tipo;

        var filtros = new List<FiltroEjecutado>();
        foreach (var nivel in definicion.Filas)
        {
            if (!request.ValoresFila.TryGetValue(nivel.Alias, out var valorJson)) continue;
            var tipo = filtroPorTipo.TryGetValue(nivel.Campo, out var t) ? t : TipoDatoReporte.Texto;
            var valor = valorJson.ValueKind == JsonValueKind.Null
                ? null
                : EjecutarReporteQueryHandler.Convertir(valorJson, tipo)
                  ?? EjecutarReporteQueryHandler.ConvertirTexto(valorJson.ToString(), tipo);
            filtros.Add(new FiltroEjecutado(nivel.Campo, tipo, OperadorFiltro.Igual, valor));
        }

        return await _motor.EjecutarAsync(reporte.QuerySql, Array.Empty<CampoFilasDef>(),
            Array.Empty<CampoValorDef>(), definicion.Columnas, filtros, definicion.Orden,
            definicion.Limite ?? 2000, reporte.Conexion, ct);
    }
}

// ---------------- Ejecución de subreporte ----------------

public sealed record EjecutarSubreporteCommand(
    Guid Id,
    int Indice,
    Dictionary<string, JsonElement> ValoresFila,
    Dictionary<string, JsonElement>? Valores,
    string Email,
    IReadOnlyList<string> Roles) : IRequest<ResultadoReporteDto>;

public sealed class EjecutarSubreporteCommandHandler : IRequestHandler<EjecutarSubreporteCommand, ResultadoReporteDto>
{
    private readonly IReporteRepository _repo;
    private readonly IMotorReportes _motor;

    public EjecutarSubreporteCommandHandler(IReporteRepository repo, IMotorReportes motor)
    {
        _repo = repo;
        _motor = motor;
    }

    public async Task<ResultadoReporteDto> Handle(EjecutarSubreporteCommand request, CancellationToken ct)
    {
        var reporte = await _repo.GetByIdAsync(request.Id, ct)
                      ?? throw new EntidadNoEncontradaException("Reporte no encontrado.");
        var esAdmin = request.Roles.Contains("Administrador", StringComparer.OrdinalIgnoreCase);
        if (!esAdmin && !reporte.EsVisiblePara(request.Email, request.Roles))
            throw new AccesoDenegadoException("No tenés permiso para ejecutar este reporte.");

        var definicion = ObtenerReporteQueryHandler.Deserializar(reporte.DefinicionJson);
        if (request.Indice < 0 || request.Indice >= definicion.Subreportes.Count)
            throw new EntidadNoEncontradaException("El subreporte indicado no existe.");

        var sub = definicion.Subreportes[request.Indice];
        var subreporte = await _repo.GetByIdAsync(sub.ReporteId, ct)
                         ?? throw new EntidadNoEncontradaException("El subreporte no existe o fue eliminado.");
        if (!subreporte.Activo)
            throw new ReglaDeNegocioException("El subreporte está inactivo.");
        var esAdminSub = request.Roles.Contains("Administrador", StringComparer.OrdinalIgnoreCase);
        if (!esAdminSub && !subreporte.EsVisiblePara(request.Email, request.Roles))
            throw new AccesoDenegadoException("No tenés permiso para el subreporte.");

        // Los valores de la fila padre filtran el subreporte por los campos de relación.
        var filtros = new List<FiltroEjecutado>();
        var subDefinicion = ObtenerReporteQueryHandler.Deserializar(subreporte.DefinicionJson);
        foreach (var rel in sub.CamposRelacion)
        {
            if (!request.ValoresFila.TryGetValue(rel.CampoPadre, out var valorPadre)) continue;
            var convertido = EjecutarReporteQueryHandler.Convertir(valorPadre, TipoDatoReporte.Texto);
            filtros.Add(new FiltroEjecutado(rel.CampoHijo, TipoDatoReporte.Texto, OperadorFiltro.Igual, convertido));
        }
        filtros.AddRange(EjecutarReporteQueryHandler.ResolverFiltros(subDefinicion.Filtros, request.Valores));

        return await _motor.EjecutarAsync(subreporte.QuerySql, subDefinicion.Filas, subDefinicion.Valores,
            subDefinicion.Columnas, filtros, subDefinicion.Orden, subDefinicion.Limite, subreporte.Conexion, ct);
    }
}

// ---------------- Diseño de apariencia (canvas) ----------------

public sealed record GuardarDisenoCommand(Guid Id, string DisenoJson, string UsuarioEmail) : IRequest;

public sealed class GuardarDisenoCommandHandler : IRequestHandler<GuardarDisenoCommand>
{
    private readonly IReporteRepository _repo;
    public GuardarDisenoCommandHandler(IReporteRepository repo) => _repo = repo;

    public async Task Handle(GuardarDisenoCommand request, CancellationToken ct)
    {
        var reporte = await _repo.GetByIdAsync(request.Id, ct)
                      ?? throw new EntidadNoEncontradaException("Reporte no encontrado.");
        var esAdmin = false;
        if (!esAdmin)
        {
            // El diseño lo puede editar el creador; los administradores validan por rol en el controller.
            if (string.IsNullOrWhiteSpace(request.UsuarioEmail))
                throw new AccesoDenegadoException("No se pudo identificar el usuario.");
        }
        reporte.EstablecerDiseno(request.DisenoJson);
        await _repo.UpdateAsync(reporte, ct);
    }
}

// ---------------- Filtros para dashboards (valores fijos, sin parametrizables) ----------------

public static class ExtensionesFiltrosDashboard
{
    public static IReadOnlyList<FiltroEjecutado> ResolverFiltrosDashboard(
        IReadOnlyList<FiltroDashboardDef> definidos, Dictionary<string, JsonElement>? valores)
    {
        var resultado = new List<FiltroEjecutado>();
        foreach (var f in definidos)
        {
            object? valor = null, valor2 = null;
            List<object?>? lista = null;

            if (valores is not null && valores.TryGetValue(f.Campo, out var elemento))
            {
                switch (f.Operador)
                {
                    case OperadorFiltro.Entre:
                        valor = EjecutarReporteQueryHandler.Convertir(elemento, f.TipoDato);
                        if (valores.TryGetValue(f.Campo + "_hasta", out var hasta))
                            valor2 = EjecutarReporteQueryHandler.Convertir(hasta, f.TipoDato);
                        break;
                    case OperadorFiltro.En:
                        lista = elemento.EnumerateArray().Select(e => EjecutarReporteQueryHandler.Convertir(e, f.TipoDato)).ToList();
                        break;
                    default:
                        valor = EjecutarReporteQueryHandler.Convertir(elemento, f.TipoDato);
                        break;
                }
            }

            var sinValor = valor is null && valor2 is null && lista is null &&
                           f.Operador is not (OperadorFiltro.Vacio or OperadorFiltro.NoVacio);
            if (sinValor) continue;
            resultado.Add(new FiltroEjecutado(f.Campo, f.TipoDato, f.Operador, valor, valor2, lista));
        }
        return resultado;
    }
}

// ---------------- Permisos ----------------

public sealed record AsignarPermisosCommand(Guid Id, IReadOnlyList<PermisoDto> Permisos, string UsuarioEmail) : IRequest;

public sealed class AsignarPermisosCommandHandler : IRequestHandler<AsignarPermisosCommand>
{
    private readonly IReporteRepository _repo;
    public AsignarPermisosCommandHandler(IReporteRepository repo) => _repo = repo;

    public async Task Handle(AsignarPermisosCommand request, CancellationToken ct)
    {
        var reporte = await _repo.GetByIdAsync(request.Id, ct)
                      ?? throw new EntidadNoEncontradaException("Reporte no encontrado.");
        if (!string.Equals(reporte.CreadoPorEmail, request.UsuarioEmail, StringComparison.OrdinalIgnoreCase))
            throw new AccesoDenegadoException("Solo el creador del reporte puede administrar sus permisos.");

        reporte.ReemplazarPermisos(request.Permisos.Select(p => (p.Email, p.Rol)));
        await _repo.UpdateAsync(reporte, ct);
    }
}
