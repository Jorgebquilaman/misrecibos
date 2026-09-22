using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PortalIUPA.Worker.Reloj.Models;

namespace PortalIUPA.Worker.Reloj.Data;

/// <summary>
/// Mapeo de esquema parametrizable: la base destino es la del software oficial del proveedor
/// (ZKBio SQL Server). Si el proveedor cambia nombres, ajustar aquí sin reescribir la lógica.
/// </summary>
public sealed class EsquemaProveedorOptions
{
    /// <summary>Tabla de marcas de asistencia. Default ZKBio: checkinout.</summary>
    public string TablaMarcas { get; set; } = "checkinout";

    /// <summary>Tabla de usuarios del dispositivo (legajo → USERID). Default ZKBio: userinfo.</summary>
    public string TablaUsuarios { get; set; } = "userinfo";

    public string ColUserId { get; set; } = "USERID";
    public string ColCheckTime { get; set; } = "CHECKTIME";
    public string ColCheckType { get; set; } = "CHECKTYPE";
    public string ColBadgeNumber { get; set; } = "BADGENUMBER";
    public string TablaCheckpoint { get; set; } = "ZkSync_Control_Checkpoint";
}

/// <summary>
/// Repositorio de asistencia en SQL Server (esquema existente del software oficial del proveedor).
/// Upsert sin duplicados: por cada registro verifica existencia (USERID + CHECKTIME + CHECKTYPE)
/// antes de insertar. Parametrizado 100%: nunca se concatena texto en los valores.
/// </summary>
public sealed class AttendanceRepository
{
    private readonly string _connectionString;
    private readonly EsquemaProveedorOptions _esquema;
    private readonly ILogger<AttendanceRepository> _logger;

    /// <summary>Caché legajo → USERID (la tabla userinfo no cambia en caliente).</summary>
    private Dictionary<int, int> _legajoAUserId = new();
    private bool _usuariosCargados;

    public AttendanceRepository(string connectionString, EsquemaProveedorOptions? esquema = null,
        ILogger<AttendanceRepository>? logger = null)
    {
        _connectionString = connectionString;
        _esquema = esquema ?? new EsquemaProveedorOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AttendanceRepository>.Instance;
    }

    /// <summary>Identificadores grabados en cada marca insertada por este worker (auditoría).</summary>
    public const string MemoOrigen = "Descarga worker ZkSync";
    public const string SensorId = "PORTAL-IUPA";
    public const string Sn = "PORTAL-IUPA";

    /// <summary>
    /// Garantiza la tabla de checkpoint (idempotente). Ejecutarlo una vez por arranque.
    /// </summary>
    public async Task AsegurarCheckpointAsync(CancellationToken ct = default)
    {
        await using var conexion = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
        await conexion.OpenAsync(ct);
        await using var cmd = new Microsoft.Data.SqlClient.SqlCommand($"""
            IF OBJECT_ID(N'dbo.{_esquema.TablaCheckpoint}', N'U') IS NULL
            CREATE TABLE dbo.{_esquema.TablaCheckpoint} (
                Dispositivo NVARCHAR(20) NOT NULL PRIMARY KEY,
                UltimoCheckpoint DATETIME NOT NULL,
                ActualizadoEn DATETIME NOT NULL DEFAULT GETDATE()
            )
            """, conexion);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<DateTime> ObtenerCheckpointAsync(string dispositivo, CancellationToken ct = default)
    {
        await using var conexion = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
        await conexion.OpenAsync(ct);
        await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(
            $"SELECT UltimoCheckpoint FROM dbo.{_esquema.TablaCheckpoint} WHERE Dispositivo = @Dispositivo", conexion);
        cmd.Parameters.AddWithValue("@Dispositivo", dispositivo);
        var resultado = await cmd.ExecuteScalarAsync(ct);
        return resultado is DateTime fecha ? fecha : DateTime.MinValue;
    }

    public async Task ActualizarCheckpointAsync(string dispositivo, DateTime checkpoint, CancellationToken ct = default)
    {
        await using var conexion = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
        await conexion.OpenAsync(ct);
        await using var cmd = new Microsoft.Data.SqlClient.SqlCommand($"""
            MERGE dbo.{_esquema.TablaCheckpoint} WITH (HOLDLOCK) AS destino
            USING (SELECT @Dispositivo AS Dispositivo) AS fuente ON destino.Dispositivo = fuente.Dispositivo
            WHEN MATCHED THEN UPDATE SET UltimoCheckpoint = @Checkpoint, ActualizadoEn = GETDATE()
            WHEN NOT MATCHED THEN INSERT (Dispositivo, UltimoCheckpoint, ActualizadoEn)
                 VALUES (@Dispositivo, @Checkpoint, GETDATE());
            """, conexion);
        cmd.Parameters.AddWithValue("@Dispositivo", dispositivo);
        cmd.Parameters.Add("@Checkpoint", SqlDbType.DateTime).Value = checkpoint;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Resuelve (y cachea) los USERID de los legajos de las marcas a insertar.</summary>
    private async Task CargarUsuariosAsync(CancellationToken ct = default)
    {
        if (_usuariosCargados) return;
        await using var conexion = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
        await conexion.OpenAsync(ct);
        // Si el sistema del proveedor usa otra columna de legajo (p. ej. BADGENUMBER/SSN/PIN),
        // es el único punto a adaptar: TablaUsuarios.ColBadgeNumber.
        await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(
            $"SELECT BADGENUMBER, USERID FROM {_esquema.TablaUsuarios}", conexion);
        await using var lector = await cmd.ExecuteReaderAsync(ct);
        _legajoAUserId.Clear();
        while (await lector.ReadAsync(ct))
        {
            if (lector.IsDBNull(0)) continue;
            if (int.TryParse(Convert.ToString(lector.GetValue(0)), out var legajo))
                _legajoAUserId[legajo] = Convert.ToInt32(lector["USERID"]);
        }
        _usuariosCargados = true;
        _logger.LogInformation("Usuarios cargados del dispositivo: {Cantidad} legajos.", _legajoAUserId.Count);
    }

    /// <summary>
    /// Inserta en la tabla de asistencia del proveedor los registros que no existan.
    /// Antiduplicado: chequeo por (USERID + CHECKTIME + CHECKTYPE) con NOT EXISTS.
    /// (Si el esquema del proveedor tiene una clave natural con índice único, este chequeo
    /// es redundante pero inofensivo; ver sql/ZkSync_Control.sql para el índice sugerido.)
    /// Devuelve la cantidad de registros insertados.
    /// </summary>
    public async Task<int> UpsertAsync(IReadOnlyList<AttendanceRecord> registros,
        string dispositivo, CancellationToken ct = default)
    {
        await CargarUsuariosAsync(ct);

        var insertadas = 0;
        await using var conexion = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
        await conexion.OpenAsync(ct);

        foreach (var registro in registros)
        {
            if (!int.TryParse(registro.Pin, out var legajo)) continue;
            if (!_legajoAUserId.TryGetValue(legajo, out var userId)) continue;

            // Convenio del proveedor para CHECKTYPE: 'I' = entrada, 'O' = salida.
            var checkType = registro.EsSalida ? "O" : "I";

            // --- Antiduplicado: (legajo + timestamp [+ tipo]) ---
            await using (var cmdExiste = new Microsoft.Data.SqlClient.SqlCommand(
                $"SELECT COUNT(1) FROM {_esquema.TablaMarcas} " +
                $"WHERE {_esquema.ColUserId} = @u AND {_esquema.ColCheckTime} = @f AND {_esquema.ColCheckType} = @t",
                conexion))
            {
                cmdExiste.Parameters.AddWithValue("@u", userId);
                cmdExiste.Parameters.Add("@f", SqlDbType.DateTime).Value = registro.Timestamp;
                cmdExiste.Parameters.AddWithValue("@t", checkType);
                if (Convert.ToInt32(await cmdExiste.ExecuteScalarAsync(ct)) > 0) continue;
            }

            // Esquema real de ZKBio. Si el proveedor usa otras columnas, adaptar acá:
            //   ALTER TABLE ... o cambiar EsquemaProveedorOptions + este INSERT.
            const string sqlInsert = """
                INSERT INTO checkinout (USERID, CHECKTIME, CHECKTYPE, VERIFYCODE, SENSORID, Memoinfo, WorkCode, sn, UserExtFmt)
                VALUES (@u, @f, @t, @v, @s, @m, 0, @sn, 1)
                """;
            await using var cmdInsert = new Microsoft.Data.SqlClient.SqlCommand(sqlInsert, conexion);
            cmdInsert.Parameters.AddWithValue("@u", userId);
            cmdInsert.Parameters.Add("@f", SqlDbType.DateTime).Value = registro.Timestamp;
            cmdInsert.Parameters.AddWithValue("@t", checkType);
            cmdInsert.Parameters.AddWithValue("@v", registro.ModoVerificacion == 0 ? 1 : registro.ModoVerificacion);
            cmdInsert.Parameters.AddWithValue("@s", SensorId);
            cmdInsert.Parameters.AddWithValue("@sn", Sn);
            cmdInsert.Parameters.AddWithValue("@m", $"Descarga worker {dispositivo}");
            insertadas += await cmdInsert.ExecuteNonQueryAsync(ct) > 0 ? 1 : 0;
        }

        return insertadas;
    }
}
