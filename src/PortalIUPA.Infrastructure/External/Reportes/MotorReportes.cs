using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;
using PortalIUPA.Application.Common;
using PortalIUPA.Application.UseCases.Reportes;

namespace PortalIUPA.Infrastructure.External.Reportes;

/// <summary>
/// Motor de ejecución de reportes: arma SQL dinámico (GROUP BY + agregados) sobre la
/// consulta base, con filtros parametrizados, y lo ejecuta en transacción de solo lectura.
/// Nunca concatena valores de filtro: todo va por NpgsqlParameter.
/// </summary>
public sealed class MotorReportes : IMotorReportes
{
    private static readonly Regex PalabrasProhibidas = new(
        @"\b(insert|update|delete|drop|alter|create|truncate|grant|revoke|exec|execute|call|do|vacuum|copy)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Conexiones disponibles por nombre (PortalIUPA + las definidas en Reportes:Conexiones).</summary>
    private readonly IReadOnlyDictionary<string, string> _conexiones;

    public MotorReportes(IReadOnlyDictionary<string, string> conexiones)
    {
        if (!conexiones.ContainsKey(PortalIUPA.Domain.Entities.ReporteDefinicion.ConexionPrincipal))
            throw new InvalidOperationException("Falta la cadena de conexión PortalIUPA.");
        _conexiones = conexiones;
    }

    public IReadOnlyList<string> ConexionesDisponibles => _conexiones.Keys.ToList();

    /// <summary>Tablas y vistas accesibles en la conexión (excluye esquemas del sistema).</summary>
    public async Task<IReadOnlyList<TablaListaDto>> ObtenerTablasAsync(string? conexion = null, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(Cadena(conexion));
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT table_schema, table_name FROM information_schema.tables " +
            "WHERE table_schema NOT IN ('pg_catalog', 'information_schema') " +
            "ORDER BY table_schema, table_name", conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var tablas = new List<TablaListaDto>();
        while (await reader.ReadAsync(ct))
            tablas.Add(new TablaListaDto(reader.GetString(0), reader.GetString(1)));
        return tablas;
    }

    /// <summary>Columnas y FKs de una tabla (para el asistente visual).</summary>
    public async Task<TablaDetalleDto> ObtenerTablaAsync(string esquema, string tabla, string? conexion = null, CancellationToken ct = default)
    {
        if (esquema.Contains(';') || tabla.Contains(';') || esquema.Contains('"') || tabla.Contains('"'))
            throw new ReglaDeNegocioException("Nombre de tabla inválido.");

        await using var conn = new NpgsqlConnection(Cadena(conexion));
        await conn.OpenAsync(ct);

        var columnas = new List<ColumnaTablaDto>();
        await using (var cmd = new NpgsqlCommand(
            "SELECT column_name, data_type FROM information_schema.columns " +
            "WHERE table_schema = @es AND table_name = @tb ORDER BY ordinal_position", conn))
        {
            cmd.Parameters.AddWithValue("es", esquema);
            cmd.Parameters.AddWithValue("tb", tabla);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                columnas.Add(new ColumnaTablaDto(reader.GetString(0), reader.GetString(1)));
        }

        var relaciones = new List<RelacionFkDto>();
        await using (var cmd = new NpgsqlCommand(
            "SELECT kcu.column_name, ccu.table_schema || '.' || ccu.table_name, ccu.column_name " +
            "FROM information_schema.table_constraints tc " +
            "JOIN information_schema.key_column_usage kcu ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema " +
            "JOIN information_schema.constraint_column_usage ccu ON ccu.constraint_name = tc.constraint_name AND ccu.table_schema = tc.table_schema " +
            "WHERE tc.constraint_type = 'FOREIGN KEY' AND tc.table_schema = @es AND tc.table_name = @tb", conn))
        {
            cmd.Parameters.AddWithValue("es", esquema);
            cmd.Parameters.AddWithValue("tb", tabla);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                relaciones.Add(new RelacionFkDto(reader.GetString(1), reader.GetString(0), reader.GetString(2)));
        }

        return new TablaDetalleDto(esquema, tabla, columnas, relaciones);
    }

    private static TipoDatoReporte MapearTipoTexto(string dataTypo) => dataTypo switch
    {
        "smallint" or "integer" or "bigint" or "numeric" or "real" or "double precision" or "money" => TipoDatoReporte.Numero,
        "timestamp without time zone" or "timestamp with time zone" or "date" or "time without time zone" => TipoDatoReporte.Fecha,
        "boolean" => TipoDatoReporte.Booleano,
        _ => TipoDatoReporte.Texto
    };

    private string Cadena(string conexion)
    {
        if (string.IsNullOrWhiteSpace(conexion)) conexion = PortalIUPA.Domain.Entities.ReporteDefinicion.ConexionPrincipal;
        if (!_conexiones.TryGetValue(conexion, out var cadena))
            throw new ReglaDeNegocioException($"La conexión '{conexion}' no está configurada en el servidor.");
        return cadena;
    }

    /// <summary>Valida que la consulta sea de solo lectura (SELECT/WITH) y sin múltiples sentencias.</summary>
    public static void ValidarSoloLectura(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ReglaDeNegocioException("La consulta está vacía.");

        var limpia = sql.Trim().TrimEnd(';');
        if (limpia.Contains(';'))
            throw new ReglaDeNegocioException("Solo se permite una única sentencia por consulta.");

        if (!limpia.StartsWith("select", StringComparison.OrdinalIgnoreCase) &&
            !limpia.StartsWith("with", StringComparison.OrdinalIgnoreCase))
            throw new ReglaDeNegocioException("Solo se permiten consultas SELECT (o WITH ... SELECT).");

        // Quitamos strings y comentarios para no falsificar prohibiciones dentro de literales.
        var sinLiterales = Regex.Replace(limpia, @"'(?:[^']|'')*'", "''");
        sinLiterales = Regex.Replace(sinLiterales, @"--[^\r\n]*", " ");
        sinLiterales = Regex.Replace(sinLiterales, @"/\*.*?\*/", " ", RegexOptions.Singleline);

        var sinSelect = Regex.Replace(sinLiterales, @"\b(select|with|as|from|where|case|when|then|else|end)\b", " ",
            RegexOptions.IgnoreCase);
        if (PalabrasProhibidas.IsMatch(sinSelect))
            throw new ReglaDeNegocioException("La consulta contiene operaciones no permitidas (solo lectura).");
    }

    /// <summary>
    /// Devuelve la metadata de columnas de la consulta (nombre y tipo lógico) sin traer filas.
    /// </summary>
    public async Task<IReadOnlyList<ColumnaMetadata>> ObtenerMetadataAsync(string sql, string? conexion = null, CancellationToken ct = default)
    {
        ValidarSoloLectura(sql);
        try
        {
            await using var conn = new NpgsqlConnection(Cadena(conexion));
            await conn.OpenAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

            var envuelta = $"SELECT * FROM ({sql.Trim().TrimEnd(';')}) _meta LIMIT 0";
            await using var cmd = new NpgsqlCommand(envuelta, conn, tx);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var schema = await reader.GetColumnSchemaAsync(ct);

            var columnas = new List<ColumnaMetadata>();
            foreach (var col in schema)
            {
                if (col.ColumnName is null) continue;
                columnas.Add(new ColumnaMetadata(col.ColumnName, MapearTipo(col.NpgsqlDbType)));
            }
            return columnas;
        }
        catch (NpgsqlException ex)
        {
            throw new ReglaDeNegocioException(
                $"No se pudo consultar la base del reporte (conexión '{conexion ?? PortalIUPA.Domain.Entities.ReporteDefinicion.ConexionPrincipal}'): {MensajeNpgsql(ex)}");
        }
        catch (TimeoutException ex)
        {
            throw new ReglaDeNegocioException(
                $"La conexión '{conexion ?? PortalIUPA.Domain.Entities.ReporteDefinicion.ConexionPrincipal}' no respondió (timeout). Verificá que el servidor de esa base sea alcanzable desde el portal.");
        }
    }

    /// <summary>Traduce los errores típicos de Npgsql a mensajes entendibles.</summary>
    private static string MensajeNpgsql(NpgsqlException ex)
    {
        if (ex is PostgresException pg)
        {
            return pg.SqlState switch
            {
                "42P01" => $"la tabla o vista no existe ({pg.MessageText})",
                "42703" => $"columna inexistente ({pg.MessageText})",
                "42501" => "permiso denegado para el usuario de la conexión",
                "28P01" => "usuario o contraseña incorrectos",
                "3D000" => "la base de datos no existe",
                "53300" => "demasiadas conexiones",
                _ => pg.MessageText
            };
        }
        return ex.InnerException?.Message ?? ex.Message;
    }

    /// <summary>
    /// Ejecuta el reporte: aplica filtros, agrupamientos y agregaciones según la definición,
    /// y devuelve las filas agrupadas + los totales generales (calculados por SQL).
    /// </summary>
    public async Task<ResultadoReporteDto> EjecutarAsync(
        string sqlBase,
        IReadOnlyList<CampoFilasDef> filas,
        IReadOnlyList<CampoValorDef> valores,
        IReadOnlyList<CampoColumnaDef> columnas,
        IReadOnlyList<FiltroEjecutado> filtros,
        string? orden,
        int? limite,
        string? conexion = null,
        CancellationToken ct = default)
    {
        ValidarSoloLectura(sqlBase);
        var cadena = Cadena(conexion);

        // Con agrupamiento Y agregaciones → SQL con GROUP BY.
        // Agrupamiento sin agregaciones → modo detalle: devolvemos las filas con las columnas elegidas
        // (agrupar sin agregar no aporta nada y oculta los datos).
        var esAgregado = filas.Count > 0 && valores.Count > 0;
        var (where, parametros) = ArmarWhere(filtros);
        var seleccion = esAgregado
            ? ArmarSelectAgregado(filas, valores)
            : ArmarSelectDetalle(columnas, filas);

        var sql = new StringBuilder($"SELECT {seleccion} FROM ({sqlBase.Trim().TrimEnd(';')}) _rep");
        if (!string.IsNullOrWhiteSpace(where)) sql.Append($" WHERE {where}");
        if (esAgregado)
        {
            var grupos = string.Join(", ", filas.Select(f => $"_rep.\"{EscaparIdent(f.Campo)}\""));
            sql.Append($" GROUP BY {grupos}");
            var ordenColumnas = string.Join(", ", filas.Select(f => $"_rep.\"{EscaparIdent(f.Campo)}\" ASC"));
            sql.Append($" ORDER BY {ordenColumnas}");
        }
        else if (!string.IsNullOrWhiteSpace(orden))
        {
            var ordenPartes = orden.Split(',').Select(o => o.Trim()).Where(o => o.Length > 0)
                .Select(o => $"\"{EscaparIdent(o)}\"");
            sql.Append($" ORDER BY {string.Join(", ", ordenPartes)}");
        }
        else if (columnas.Count > 0)
        {
            sql.Append($" ORDER BY _rep.\"{EscaparIdent(columnas[0].Alias)}\"");
        }
        if (limite is > 0) sql.Append($" LIMIT {limite.Value}");

        var filasResultado = await EjecutarSqlAsync(cadena, sql.ToString(), parametros, ct);

        var totales = new Dictionary<string, object?>();
        if (valores.Count > 0)
        {
            var totalesSelect = string.Join(", ", valores.Select(v =>
                $"{FuncionAgregacion(v.Agregacion)}(_rep.\"{EscaparIdent(v.Campo)}\") AS \"{EscaparIdent(v.Alias)}\""));
            var sqlTotales = $"SELECT {totalesSelect} FROM ({sqlBase.Trim().TrimEnd(';')}) _rep";
            if (!string.IsNullOrWhiteSpace(where)) sqlTotales += $" WHERE {where}";
            var filasTotales = await EjecutarSqlAsync(cadena, sqlTotales, parametros, ct);
            if (filasTotales.Count > 0)
                foreach (var v in valores)
                    totales[v.Alias] = filasTotales[0][v.Alias];
        }

        var nombresColumnas = filasResultado.Count > 0 ? filasResultado[0].Keys.ToList() : new List<string>();
        return new ResultadoReporteDto(nombresColumnas, filasResultado, totales);
    }

    private static string ArmarSelectAgregado(IReadOnlyList<CampoFilasDef> filas, IReadOnlyList<CampoValorDef> valores)
    {
        var partes = filas.Select(f => $"_rep.\"{EscaparIdent(f.Campo)}\" AS \"{EscaparIdent(f.Alias)}\"").ToList();
        partes.AddRange(valores.Select(v =>
            $"{FuncionAgregacion(v.Agregacion)}(_rep.\"{EscaparIdent(v.Campo)}\") AS \"{EscaparIdent(v.Alias)}\""));
        return string.Join(", ", partes);
    }

    private static string ArmarSelectDetalle(IReadOnlyList<CampoColumnaDef> columnas, IReadOnlyList<CampoFilasDef> filas)
    {
        // Siempre incluimos los campos de agrupamiento primero: sin ellos el visor no puede agrupar.
        var elegidas = new List<string>();
        var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in filas)
            if (usados.Add(f.Campo))
                elegidas.Add($"_rep.\"{EscaparIdent(f.Campo)}\" AS \"{EscaparIdent(f.Alias)}\"");
        foreach (var c in columnas)
            if (usados.Add(c.Campo))
                elegidas.Add($"_rep.\"{EscaparIdent(c.Campo)}\" AS \"{EscaparIdent(c.Alias)}\"");
        return elegidas.Count > 0 ? string.Join(", ", elegidas) : "*";
    }

    /// <summary>Construye el WHERE con placeholders @p0..@pN a partir de los filtros resueltos.</summary>
    private static (string Where, List<NpgsqlParameter> Parametros) ArmarWhere(IReadOnlyList<FiltroEjecutado> filtros)
    {
        var condiciones = new List<string>();
        var parametros = new List<NpgsqlParameter>();
        var i = 0;

        void AgregarParam(object? valor, NpgsqlDbType tipo)
        {
            var p = new NpgsqlParameter($"p{i}", tipo) { Value = valor ?? DBNull.Value };
            parametros.Add(p);
            i++;
        }

        foreach (var f in filtros)
        {
            if (f.Valores is { Count: > 0 })
            {
                var nombres = new List<string>();
                foreach (var valor in f.Valores)
                {
                    AgregarParam(valor, TipoNpgsql(f.TipoDato));
                    nombres.Add($"@p{i - 1}");
                }
                condiciones.Add($"_rep.\"{EscaparIdent(f.Campo)}\" IN ({string.Join(", ", nombres)})");
                continue;
            }

            if (f.Operador == OperadorFiltro.Entre)
            {
                AgregarParam(f.Valor, TipoNpgsql(f.TipoDato));
                AgregarParam(f.Valor2, TipoNpgsql(f.TipoDato));
                condiciones.Add(
                    $"_rep.\"{EscaparIdent(f.Campo)}\" >= @p{i - 2} AND _rep.\"{EscaparIdent(f.Campo)}\" <= @p{i - 1}");
                continue;
            }

            switch (f.Operador)
            {
                case OperadorFiltro.Igual:
                    AgregarParam(f.Valor, TipoNpgsql(f.TipoDato));
                    condiciones.Add($"_rep.\"{EscaparIdent(f.Campo)}\" = @p{i - 1}");
                    break;
                case OperadorFiltro.Distinto:
                    AgregarParam(f.Valor, TipoNpgsql(f.TipoDato));
                    condiciones.Add($"(_rep.\"{EscaparIdent(f.Campo)}\" <> @p{i - 1} OR _rep.\"{EscaparIdent(f.Campo)}\" IS NULL)");
                    break;
                case OperadorFiltro.Contiene:
                    AgregarParam($"%{f.Valor}%", NpgsqlDbType.Text);
                    condiciones.Add($"_rep.\"{EscaparIdent(f.Campo)}\"::text ILIKE @p{i - 1}");
                    break;
                case OperadorFiltro.NoContiene:
                    AgregarParam($"%{f.Valor}%", NpgsqlDbType.Text);
                    condiciones.Add($"_rep.\"{EscaparIdent(f.Campo)}\"::text NOT ILIKE @p{i - 1}");
                    break;
                case OperadorFiltro.Mayor:
                    AgregarParam(f.Valor, TipoNpgsql(f.TipoDato));
                    condiciones.Add($"_rep.\"{EscaparIdent(f.Campo)}\" > @p{i - 1}");
                    break;
                case OperadorFiltro.MayorIgual:
                    AgregarParam(f.Valor, TipoNpgsql(f.TipoDato));
                    condiciones.Add($"_rep.\"{EscaparIdent(f.Campo)}\" >= @p{i - 1}");
                    break;
                case OperadorFiltro.Menor:
                    AgregarParam(f.Valor, TipoNpgsql(f.TipoDato));
                    condiciones.Add($"_rep.\"{EscaparIdent(f.Campo)}\" < @p{i - 1}");
                    break;
                case OperadorFiltro.MenorIgual:
                    AgregarParam(f.Valor, TipoNpgsql(f.TipoDato));
                    condiciones.Add($"_rep.\"{EscaparIdent(f.Campo)}\" <= @p{i - 1}");
                    break;
                case OperadorFiltro.NoVacio:
                    condiciones.Add($"_rep.\"{EscaparIdent(f.Campo)}\" IS NOT NULL");
                    break;
                case OperadorFiltro.Vacio:
                    condiciones.Add($"_rep.\"{EscaparIdent(f.Campo)}\" IS NULL");
                    break;
            }
        }

        return (condiciones.Count > 0 ? string.Join(" AND ", condiciones) : string.Empty, parametros);
    }

    private async Task<List<Dictionary<string, object?>>> EjecutarSqlAsync(
        string cadena, string sql, List<NpgsqlParameter> parametros, CancellationToken ct)
    {
        NpgsqlConnection conn;
        try
        {
            conn = new NpgsqlConnection(cadena);
            await conn.OpenAsync(ct);
        }
        catch (NpgsqlException ex)
        {
            throw new ReglaDeNegocioException(
                $"No se pudo conectar a la base del reporte: {MensajeNpgsql(ex)}");
        }
        catch (TimeoutException)
        {
            throw new ReglaDeNegocioException("La conexión a la base del reporte no respondió (timeout).");
        }
        await using (conn)
        {
            await using var tx = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            await using var soloLectura = new NpgsqlCommand("SET TRANSACTION READ ONLY", conn, tx);
            await soloLectura.ExecuteNonQueryAsync(ct);

            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            foreach (var p in parametros) cmd.Parameters.Add(p.Clone());
            NpgsqlDataReader reader;
            try
            {
                reader = await cmd.ExecuteReaderAsync(ct);
            }
            catch (PostgresException ex) when (ex.SqlState == "42703")
            {
                throw new ReglaDeNegocioException(
                    $"La consulta usa una columna que no existe dentro de la subconsulta ({ex.MessageText}). " +
                    "Revisá que las columnas del diseño coincidan con los nombres que devuelve la consulta " +
                    "(si usaste alias, el nombre real dentro de la subconsulta es el alias).");
            }
            catch (PostgresException ex)
            {
                throw new ReglaDeNegocioException($"Error de la base al ejecutar la consulta: {MensajeNpgsql(ex)}");
            }
            await using (reader)
            {
                var resultado = new List<Dictionary<string, object?>>();
                while (await reader.ReadAsync(ct))
                {
                    var fila = new Dictionary<string, object?>(StringComparer.Ordinal);
                    for (var c = 0; c < reader.FieldCount; c++)
                        fila[reader.GetName(c)] = reader.IsDBNull(c) ? null : reader.GetValue(c);
                    resultado.Add(fila);
                }
                return resultado;
            }
        }
    }

    internal static string FuncionAgregacion(AgregacionReporte agregacion) => agregacion switch
    {
        AgregacionReporte.Suma => "SUM",
        AgregacionReporte.Promedio => "AVG",
        AgregacionReporte.Maximo => "MAX",
        AgregacionReporte.Minimo => "MIN",
        AgregacionReporte.Conteo => "COUNT",
        _ => throw new ReglaDeNegocioException($"Agregación no soportada: {agregacion}")
    };

    private static NpgsqlDbType TipoNpgsql(TipoDatoReporte tipo) => tipo switch
    {
        TipoDatoReporte.Numero => NpgsqlDbType.Numeric,
        TipoDatoReporte.Fecha => NpgsqlDbType.Timestamp,
        TipoDatoReporte.Booleano => NpgsqlDbType.Boolean,
        _ => NpgsqlDbType.Text
    };

    /// <summary>Escapa comillas dobles de identificadores entre comillas (previene inyección por nombre de columna).</summary>
    private static string EscaparIdent(string identificador) =>
        (identificador ?? string.Empty).Replace("\"", "\"\"");

    private static TipoDatoReporte MapearTipo(NpgsqlDbType? tipo) => tipo switch
    {
        NpgsqlDbType.Smallint or NpgsqlDbType.Integer or NpgsqlDbType.Bigint or NpgsqlDbType.Numeric
            or NpgsqlDbType.Real or NpgsqlDbType.Double or NpgsqlDbType.Money => TipoDatoReporte.Numero,
        NpgsqlDbType.Timestamp or NpgsqlDbType.TimestampTz or NpgsqlDbType.Date or NpgsqlDbType.Time
            or NpgsqlDbType.TimeTz or NpgsqlDbType.Interval => TipoDatoReporte.Fecha,
        NpgsqlDbType.Boolean => TipoDatoReporte.Booleano,
        _ => TipoDatoReporte.Texto
    };
}
