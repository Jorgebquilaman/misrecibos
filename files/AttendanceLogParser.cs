using System.Buffers.Binary;
using Microsoft.Extensions.Logging;
using ZkAttendanceService.Models;

namespace ZkAttendanceService.Device;

/// <summary>
/// Parsea el buffer crudo devuelto por CMD_ATTLOG_RRQ a registros de asistencia.
///
/// IMPORTANTE: el layout de 40 bytes por registro (PIN de 24 bytes + modo de
/// verificación + status + timestamp empaquetado en 4 bytes) es el más común entre
/// relojes standalone ZK clásicos, pero puede variar levemente según la versión de
/// firmware del 628C puntual. Antes de confiar en esto en producción: hacé una
/// marcación de prueba, descargá el log, y compará con <see cref="DumpRecordHex"/>
/// que el PIN y el horario caen donde se espera. Si el tamaño del buffer no es
/// múltiplo de 40, es la primera señal de que el offset real es otro.
/// </summary>
public static class AttendanceLogParser
{
    private const int RecordSize = 40;

    public static IReadOnlyList<AttendanceRecord> Parse(byte[] raw, ILogger logger)
    {
        var records = new List<AttendanceRecord>();

        if (raw.Length % RecordSize != 0)
        {
            logger.LogWarning(
                "El buffer de asistencia ({Length} bytes) no es múltiplo de {RecordSize}. " +
                "El tamaño de registro real de este firmware podría ser distinto: revisar " +
                "con una captura real antes de confiar en el parseo.",
                raw.Length, RecordSize);
        }

        int count = raw.Length / RecordSize;
        for (int i = 0; i < count; i++)
        {
            var record = raw.AsSpan(i * RecordSize, RecordSize);

            var pin = System.Text.Encoding.ASCII.GetString(record[..24]).TrimEnd('\0', ' ');
            byte verifyMode = record[24];
            byte status = record[25];
            uint packedTime = BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(26, 4));

            records.Add(new AttendanceRecord(pin, DecodeTime(packedTime), verifyMode, status));
        }

        return records;
    }

    /// <summary>
    /// Decodifica el timestamp empaquetado en 32 bits que usan los relojes ZK
    /// (segundos/minutos/horas normales, pero con meses de 31 días y años contados
    /// desde 2000 — esquema propio del firmware, no un Unix timestamp).
    /// </summary>
    private static DateTime DecodeTime(uint t)
    {
        int second = (int)(t % 60); t /= 60;
        int minute = (int)(t % 60); t /= 60;
        int hour = (int)(t % 24); t /= 24;
        int day = (int)(t % 31) + 1; t /= 31;
        int month = (int)(t % 12) + 1; t /= 12;
        int year = (int)t + 2000;

        return new DateTime(year, month, day, hour, minute, second);
    }

    /// <summary>Volcado hexadecimal de un registro puntual, para validar offsets a mano.</summary>
    public static string DumpRecordHex(byte[] raw, int index)
    {
        var record = raw.AsSpan(index * RecordSize, RecordSize);
        return Convert.ToHexString(record);
    }
}
