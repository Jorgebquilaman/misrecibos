using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ZkAttendanceService.Models;

namespace ZkAttendanceService.Data;

/// <summary>
/// Persiste los registros de asistencia en la base SQL Server ya usada por el
/// software del proveedor.
///
/// AJUSTAR: la tabla/columnas de abajo (dbo.CHECKINOUT / USERID / CHECKTIME /
/// VERIFYCODE / SENSORID) son el esquema típico de las apps ZKTime/ZKBio, usado
/// acá como placeholder razonable. Reemplazar por los nombres reales que tenga la
/// base del proveedor antes de correr en producción — conviene inspeccionar el
/// esquema existente primero en vez de asumir que coincide.
/// </summary>
public sealed class AttendanceRepository
{
    private readonly string _connectionString;
    private readonly ILogger<AttendanceRepository> _logger;

    public AttendanceRepository(string connectionString, ILogger<AttendanceRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<int> UpsertAsync(IReadOnlyList<AttendanceRecord> records, CancellationToken ct)
    {
        if (records.Count == 0) return 0;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);

        int affected = 0;

        const string sql = """
            MERGE dbo.CHECKINOUT AS target
            USING (SELECT @UserId AS UserId, @CheckTime AS CheckTime) AS src
                ON target.USERID = src.UserId AND target.CHECKTIME = src.CheckTime
            WHEN NOT MATCHED THEN
                INSERT (USERID, CHECKTIME, VERIFYCODE, SENSORID)
                VALUES (src.UserId, src.CheckTime, @VerifyMode, @DeviceSerial);
            """;

        foreach (var record in records)
        {
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@UserId", record.EnrollNumber);
            command.Parameters.AddWithValue("@CheckTime", record.Timestamp);
            command.Parameters.AddWithValue("@VerifyMode", record.VerifyMode);
            command.Parameters.AddWithValue("@DeviceSerial", "628C-192.168.1.204");

            affected += await command.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);

        _logger.LogInformation("Insertados {Count} registros nuevos de asistencia.", affected);
        return affected;
    }
}
