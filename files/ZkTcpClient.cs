using System.Buffers.Binary;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace ZkAttendanceService.Device;

/// <summary>
/// Cliente que habla directamente el protocolo binario estándar ZK sobre TCP,
/// sin depender de zkemkeeper.dll (COM, solo Windows). Cubre lo necesario para
/// descargar el log de asistencia: conectar, autenticar si hace falta,
/// deshabilitar/habilitar el dispositivo alrededor de la lectura, y pedir el log.
/// </summary>
public sealed class ZkTcpClient : IAsyncDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly int? _commKey;
    private readonly int _timeoutMs;
    private readonly ILogger<ZkTcpClient> _logger;

    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private ushort _sessionId;
    private ushort _replyId;

    public ZkTcpClient(string host, int port, int? commKey, int timeoutMs, ILogger<ZkTcpClient> logger)
    {
        _host = host;
        _port = port;
        _commKey = commKey;
        _timeoutMs = timeoutMs;
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _tcpClient = new TcpClient();

        using var timeoutCts = new CancellationTokenSource(_timeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        await _tcpClient.ConnectAsync(_host, _port, linkedCts.Token);

        _stream = _tcpClient.GetStream();
        _stream.ReadTimeout = _timeoutMs;
        _stream.WriteTimeout = _timeoutMs;

        _sessionId = 0;
        _replyId = 0;

        var reply = await SendCommandAsync(ZkCommand.CMD_CONNECT, ct: ct);

        if (reply.Command == ZkCommand.CMD_ACK_UNAUTH)
        {
            if (_commKey is null)
                throw new InvalidOperationException(
                    "El dispositivo exige autenticación (comm key) y no se configuró ninguna en ZkDevice:CommKey.");

            _sessionId = reply.SessionId;
            var authKey = BuildAuthKey(_commKey.Value, _sessionId);
            var authReply = await SendCommandAsync(ZkCommand.CMD_AUTH, authKey, ct);

            if (authReply.Command != ZkCommand.CMD_ACK_OK)
                throw new InvalidOperationException("Autenticación rechazada por el dispositivo (comm key incorrecta).");

            reply = authReply;
        }
        else if (reply.Command != ZkCommand.CMD_ACK_OK)
        {
            throw new InvalidOperationException($"El dispositivo rechazó la conexión (comando de respuesta {reply.Command}).");
        }

        _sessionId = reply.SessionId;
        _logger.LogInformation("Conectado al reloj {Host}:{Port}, sesión {SessionId}.", _host, _port, _sessionId);
    }

    public async Task DisableDeviceAsync(CancellationToken ct = default)
    {
        var reply = await SendCommandAsync(ZkCommand.CMD_DISABLEDEVICE, ct: ct);
        if (reply.Command != ZkCommand.CMD_ACK_OK)
            _logger.LogWarning("El dispositivo no confirmó CMD_DISABLEDEVICE (respuesta {Command}).", reply.Command);
    }

    public async Task EnableDeviceAsync(CancellationToken ct = default)
    {
        var reply = await SendCommandAsync(ZkCommand.CMD_ENABLEDEVICE, ct: ct);
        if (reply.Command != ZkCommand.CMD_ACK_OK)
            _logger.LogWarning("El dispositivo no confirmó CMD_ENABLEDEVICE (respuesta {Command}).", reply.Command);
    }

    /// <summary>
    /// Descarga el buffer crudo del log de asistencia (CMD_ATTLOG_RRQ). El parseo a
    /// registros se hace aparte en <see cref="AttendanceLogParser"/>, porque el
    /// layout exacto de cada entrada puede variar según firmware.
    /// </summary>
    public async Task<byte[]> DownloadAttendanceLogRawAsync(CancellationToken ct = default)
    {
        var reply = await SendCommandAsync(ZkCommand.CMD_ATTLOG_RRQ, ct: ct);

        if (reply.Command == ZkCommand.CMD_ACK_ERROR)
            throw new InvalidOperationException("El dispositivo devolvió un error al pedir el log de asistencia.");

        if (reply.Command == ZkCommand.CMD_ACK_OK && reply.Payload.Length == 0)
        {
            _logger.LogInformation("El dispositivo no reportó nuevos registros de asistencia.");
            return Array.Empty<byte>();
        }

        if (reply.Command == ZkCommand.CMD_PREPARE_DATA)
        {
            var totalSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(reply.Payload.AsSpan(0, 4));
            _logger.LogInformation("El dispositivo anuncia {Bytes} bytes de datos de asistencia.", totalSize);

            var buffer = new byte[totalSize];
            await ReadExactAsync(buffer, 0, totalSize, ct);

            // Tras el bloque de datos crudo el dispositivo suele cerrar con un
            // paquete de encabezado (CMD_ACK_OK). Se lee y descarta sin romper el
            // flujo si no llega exactamente como se espera.
            try
            {
                var closingReply = await ReadReplyAsync(ct);
                if (closingReply.Command != ZkCommand.CMD_ACK_OK)
                    _logger.LogWarning("Cierre de transferencia inesperado (comando {Command}).", closingReply.Command);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo leer el paquete de cierre tras la transferencia de datos.");
            }

            return buffer;
        }

        // Todo el log vino en un único paquete (log chico).
        return reply.Payload;
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_stream is null) return;

        try
        {
            await SendCommandAsync(ZkCommand.CMD_EXIT, ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error enviando CMD_EXIT (se ignora, se cierra el socket de todos modos).");
        }
    }

    private async Task<ZkReply> SendCommandAsync(ushort command, byte[]? payload = null, CancellationToken ct = default)
    {
        if (_stream is null) throw new InvalidOperationException("No conectado.");

        _replyId++;
        var packet = ZkPacket.BuildCommand(command, _sessionId, _replyId, payload);
        await _stream.WriteAsync(packet, ct);

        return await ReadReplyAsync(ct);
    }

    private async Task<ZkReply> ReadReplyAsync(CancellationToken ct)
    {
        if (_stream is null) throw new InvalidOperationException("No conectado.");

        var tcpHeader = new byte[8];
        await ReadExactAsync(tcpHeader, 0, 8, ct);

        var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(tcpHeader.AsSpan(4, 4));
        var packet = new byte[length];
        await ReadExactAsync(packet, 0, length, ct);

        return ZkPacket.ParseReply(packet);
    }

    private async Task ReadExactAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        int read = 0;
        while (read < count)
        {
            int n = await _stream!.ReadAsync(buffer.AsMemory(offset + read, count - read), ct);
            if (n == 0) throw new IOException("El reloj cerró la conexión de forma inesperada durante la lectura.");
            read += n;
        }
    }

    /// <summary>
    /// Deriva la comm key de 4 bytes que espera CMD_AUTH a partir de la password
    /// numérica y el session id (algoritmo "MakeKey", público y documentado por
    /// las distintas implementaciones open source del protocolo ZK). Solo se
    /// ejercita si el dispositivo tiene una comm key configurada: si CommKey queda
    /// en null y el reloj no la exige, este camino nunca se usa.
    /// </summary>
    private static byte[] BuildAuthKey(int key, ushort sessionId)
    {
        uint k = 0;
        for (int i = 0; i < 32; i++)
        {
            k = ((key & (1 << i)) != 0) ? (uint)((k << 1) | 1) : (k << 1);
        }
        k += sessionId;

        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, k);

        byte c0 = (byte)(b[0] ^ (byte)'Z');
        byte c1 = (byte)(b[1] ^ (byte)'K');
        byte c2 = (byte)(b[2] ^ (byte)'S');
        byte c3 = (byte)(b[3] ^ (byte)'O');

        // Swap de las dos palabras de 16 bits: [c2 c3 c0 c1]
        byte d0 = c2, d1 = c3, d2 = c0, d3 = c1;

        const byte ticks = 50;
        byte e0 = (byte)(d0 ^ ticks);
        byte e1 = (byte)(d1 ^ ticks);
        byte e2 = ticks; // reemplaza a d2 directamente, no es un XOR
        byte e3 = (byte)(d3 ^ ticks);

        return new[] { e0, e1, e2, e3 };
    }

    public ValueTask DisposeAsync()
    {
        _stream?.Dispose();
        _tcpClient?.Dispose();
        return ValueTask.CompletedTask;
    }
}
