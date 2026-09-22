using System.Buffers.Binary;
using System.Net.Sockets;

namespace PortalIUPA.Infrastructure.External.ZK;

/// <summary>Marca de asistencia leída de un reloj ZKTeco.</summary>
public sealed record MarcaZkCruda(string Legajo, DateTime FechaHora, int VerifyMode, int Estado);

/// <summary>Contadores de memoria de un reloj ZKTeco (o del origen en modo MSSQL).</summary>
public sealed record RelojZkInfo(string? Nombre, string? Serial, int Usuarios, int Marcas, DateTime? UltimaMarca = null);

/// <summary>
/// Cliente del protocolo ZK (ZKTeco standalone) sobre TCP. Implementación propia sin dependencias
/// externas (el SDK oficial zkemkeeper es solo Windows), fiel al comportamiento de pyzk:
/// framing TCP "50 50 82 7d" + largo, paquete interno comando/checksum/sesión/reply_id,
/// lectura de asistencia por buffer (CMD_PREPARE_BUFFER + chunks) y decodificación de tiempos ZK.
/// Referencias: https://github.com/adrobinoga/zk-protocol y https://github.com/fananimi/pyzk.
/// </summary>
public sealed class ZkDeviceClient : IDisposable
{
    private const ushort CmdConnect = 1000;
    private const ushort CmdExit = 1001;
    private const ushort CmdEnableDevice = 1002;
    private const ushort CmdDisableDevice = 1003;
    private const ushort CmdOptionsRrq = 11;
    private const ushort CmdAttlogRrq = 13;
    private const ushort CmdClearAttlog = 15;
    private const ushort CmdGetFreeSizes = 50;
    private const ushort CmdRefreshData = 1013;
    private const ushort CmdAuth = 1102;
    private const ushort CmdPrepareData = 1500;
    private const ushort CmdData = 1501;
    private const ushort CmdFreeData = 1502;
    private const ushort CmdPrepareBuffer = 1503;
    private const ushort CmdReadBuffer = 1504;
    private const ushort AckOk = 2000;
    private const ushort AckError = 2001;
    private const ushort AckData = 2002;
    private const ushort AckUnauth = 2005;

    private readonly TcpClient _tcp;
    private readonly NetworkStream _stream;
    private ushort _sessionId;
    private ushort _replyId;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private ZkDeviceClient(TcpClient tcp, NetworkStream stream)
    {
        _tcp = tcp;
        _stream = stream;
    }

    public static async Task<ZkDeviceClient> ConectarAsync(string ip, int puerto, int commKey = 0,
        CancellationToken ct = default)
    {
        // Este firmware se cuelga períodos largos y luego despierta; reintentar aprovecha esas ventanas.
        Exception? ultimo = null;
        for (var intento = 1; intento <= 3; intento++)
        {
            try
            {
                return await ConectarInternoAsync(ip, puerto, commKey, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException && intento < 3)
            {
                ultimo = ex;
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
        throw ultimo!;
    }

    private static async Task<ZkDeviceClient> ConectarInternoAsync(string ip, int puerto, int commKey,
        CancellationToken ct)
    {
        var tcp = new TcpClient();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                await tcp.ConnectAsync(ip, puerto, timeout.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new InvalidOperationException(
                    $"No se pudo conectar al reloj {ip}:{puerto} (timeout de 5 segundos). " +
                    "Verificá la IP/puerto y que el servidor llegue por red al equipo.");
            }
            var stream = tcp.GetStream();
            stream.ReadTimeout = 15_000;
            stream.WriteTimeout = 10_000;

            var cliente = new ZkDeviceClient(tcp, stream)
            {
                _sessionId = 0,
                _replyId = unchecked(ushort.MaxValue - 1)
            };

            var respuesta = await cliente.EnviarComandoAsync(CmdConnect, Array.Empty<byte>(), ct);
            cliente._sessionId = respuesta.Sesion;
            if (respuesta.Comando == AckUnauth)
            {
                if (commKey <= 0)
                    throw new InvalidOperationException("El reloj requiere CommKey (clave de comunicación). Configurala en el reloj.");
                respuesta = await cliente.EnviarComandoAsync(CmdAuth, MakeCommKey(commKey, respuesta.Sesion), ct);
            }
            if (!respuesta.Ok)
                throw new InvalidOperationException($"El reloj rechazó la conexión (código {respuesta.Comando}).");

            return cliente;
        }
        catch
        {
            tcp.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Lee todas las marcas de asistencia con el flujo clásico directo (CMD_ATTLOG_RRQ → paquetes
    /// CMD_DATA → CMD_ACK_OK), igual que ReadGeneralLogData del SDK oficial. No usa el mecanismo
    /// CMD_PREPARE_BUFFER: en este firmware (Ver 6.60) esa secuencia traba el equipo y deja de
    /// responder hasta reiniciarlo.
    /// Detalle crítico: tras cada paquete de datos el CLIENTE debe enviar su CMD_ACK_OK; si no,
    /// el equipo queda esperándolo y su módulo de comunicación se traba.
    /// </summary>
    public async Task<IReadOnlyList<MarcaZkCruda>> LeerMarcasAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            using var vencimiento = CancellationTokenSource.CreateLinkedTokenSource(ct);
            vencimiento.CancelAfter(TimeSpan.FromSeconds(60));
            var token = vencimiento.Token;

            // IMPORTANTE: este firmware (Ver 6.60) traba su módulo de comunicación si se le pide
            // leer marcas con el log VACÍO. Como pyzk: consultar el contador y no enviar CMD_ATTLOG_RRQ
            // cuando no hay registros.
            var cantidades = await LeerCantidadesAsync(token);
            if (cantidades.Marcas <= 0)
                return Array.Empty<MarcaZkCruda>();

            var primera = await EnviarInternoAsync(CmdAttlogRrq, Array.Empty<byte>(), token);
            var datos = new List<byte>();

            if (primera.Comando == CmdPrepareData)
            {
                // Variante de este firmware: CMD_PREPARE_DATA con [4 bytes de tamaño][primeros datos],
                // el resto del bloque llega crudo. Al terminar, ACK del cliente y cierre del equipo.
                var esperado = primera.Payload.Length >= 4
                    ? BinaryPrimitives.ReadInt32LittleEndian(primera.Payload.AsSpan(0, 4))
                    : 0;
                datos.AddRange(primera.Payload.Length > 4 ? primera.Payload[4..] : Array.Empty<byte>());
                while (datos.Count < esperado)
                {
                    var faltan = esperado - datos.Count;
                    var temporal = new byte[faltan];
                    var leido = 0;
                    while (leido < faltan)
                    {
                        var n = await _stream.ReadAsync(temporal.AsMemory(leido, faltan - leido), token);
                        if (n == 0) throw new IOException("El reloj cerró la conexión durante la lectura.");
                        leido += n;
                    }
                    datos.AddRange(temporal);
                }
                await EnviarAckOkClienteAsync(token);
                // Cierre: con log vacío algunos firmwares no envían el ACK final; tolerar timeout corto.
                try
                {
                    using var cierreTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                    cierreTimeout.CancelAfter(TimeSpan.FromSeconds(3));
                    var cierre = await LeerMensajeAsync(cierreTimeout.Token);
                    if (cierre.Comando != AckOk && cierre.Comando != AckData && cierre.Payload.Length == 0)
                        throw new IOException($"Cierre inesperado de la lectura de marcas (código {cierre.Comando}).");
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    // Sin cierre del equipo (log vacío o firmware que no manda ACK final): es válido,
                    // ya tenemos todos los bytes del bloque.
                }
            }
            else if (primera.Comando == CmdData)
            {
                // Variante clásica: paquetes CMD_DATA; tras cada uno el cliente confirma con CMD_ACK_OK.
                var comando = primera.Comando;
                var mensaje = primera;
                while (comando == CmdData)
                {
                    datos.AddRange(mensaje.Payload);
                    await EnviarAckOkClienteAsync(token);
                    var siguiente = await LeerMensajeAsync(token);
                    comando = siguiente.Comando;
                    if (comando == AckOk) break;
                    if (comando != CmdData)
                        throw new IOException($"Respuesta inesperada durante la lectura de marcas (código {comando}).");
                    mensaje = siguiente;
                }
            }
            // AckOk inicial: no hay marcas.

            var tamanioRegistro = InferirTamanioRegistro(datos);
            if (tamanioRegistro == 0) return Array.Empty<MarcaZkCruda>();

            var marcas = new List<MarcaZkCruda>();
            var offset = 0;
            while (offset + tamanioRegistro <= datos.Count)
            {
                // Marcador de inicio de registro en algunos firmwares: ff 32 35 35 00 ...
                if (offset + 9 <= datos.Count &&
                    datos[offset] == 0xff && datos[offset + 1] == 0x32 && datos[offset + 2] == 0x35 &&
                    datos[offset + 3] == 0x35 && datos[offset + 4] == 0x00)
                {
                    offset += 9;
                    continue;
                }

                var marca = tamanioRegistro switch
                {
                    8 => ParseRegistro8(datos.ToArray(), offset),
                    16 => ParseRegistro16(datos.ToArray(), offset),
                    _ => ParseRegistro40(datos.ToArray(), offset)
                };
                if (marca is not null) marcas.Add(marca);
                offset += tamanioRegistro;
            }
            return marcas;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new IOException("El reloj dejó de responder durante la lectura de marcas (timeout).");
        }
        finally
        {
            _lock.Release();
        }
    }

    private static int InferirTamanioRegistro(List<byte> datos)
    {
        if (datos.Count == 0) return 0;
        if (datos.Count % 40 == 0) return 40;
        if (datos.Count % 16 == 0) return 16;
        if (datos.Count % 8 == 0) return 8;
        return 40;
    }

    /// <summary>Elimina TODOS los registros de asistencia del equipo (no toca huellas ni usuarios).</summary>
    public async Task VaciarMarcasAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await DeshabilitarAsync(ct);
            try
            {
                await EnviarComandoAsync(CmdClearAttlog, Array.Empty<byte>(), ct);
                await EnviarComandoAsync(CmdRefreshData, Array.Empty<byte>(), ct);
            }
            finally
            {
                await HabilitarSilenciosoAsync(ct);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Datos básicos del equipo (nombre, serial, usuarios, marcas en memoria).</summary>
    public async Task<RelojZkInfo> ObtenerInfoAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var nombre = await LeerOpcionAsync("~DeviceName", ct);
            var serial = await LeerOpcionAsync("~SerialNumber", ct);
            var cantidades = await LeerCantidadesAsync(ct);
            return new RelojZkInfo(nombre, serial, cantidades.Usuarios, cantidades.Marcas);
        }
        finally
        {
            _lock.Release();
        }
    }

    // ---------------------------------------------------------------- estructura de registros

    /// <summary>Registro de 8 bytes (equipos antiguos): uid(2) + estado(1) + tiempo(4) + punch(1).</summary>
    private static MarcaZkCruda? ParseRegistro8(byte[] r, int o)
    {
        var legajo = BinaryPrimitives.ReadUInt16LittleEndian(r.AsSpan(o)).ToString();
        return new MarcaZkCruda(legajo, DecodificarTiempoZk(BinaryPrimitives.ReadUInt32LittleEndian(r.AsSpan(o + 3, 4))), r[o + 2], r[o + 7]);
    }

    /// <summary>Registro de 16 bytes: uid(4) + tiempo(4) + estado(1) + punch(1) + reservado(2) + workcode(4).</summary>
    private static MarcaZkCruda? ParseRegistro16(byte[] r, int o)
    {
        var legajo = BinaryPrimitives.ReadUInt32LittleEndian(r.AsSpan(o)).ToString();
        return new MarcaZkCruda(legajo, DecodificarTiempoZk(BinaryPrimitives.ReadUInt32LittleEndian(r.AsSpan(o + 4, 4))), r[o + 8], r[o + 9]);
    }

    /// <summary>Registro TFT de 40 bytes: uid(2) + user_id(24) + estado(1)@26 + tiempo(4)@27 + punch(1)@31.</summary>
    private static MarcaZkCruda? ParseRegistro40(byte[] r, int o)
    {
        var legajo = System.Text.Encoding.ASCII.GetString(r, o + 2, 24).Split('\0')[0].Trim();
        if (legajo.Length == 0) return null;
        return new MarcaZkCruda(legajo, DecodificarTiempoZk(BinaryPrimitives.ReadUInt32LittleEndian(r.AsSpan(o + 27, 4))), r[o + 26], r[o + 31]);
    }

    /// <summary>Codificación de tiempo ZK: segundo + min*60 + hora*3600 + día*86400 + mes*2678400 + (año-2000)*32140800.</summary>
    internal static DateTime DecodificarTiempoZk(uint valor)
    {
        var segundo = valor % 60;
        var minuto = (valor / 60) % 60;
        var hora = (valor / 3600) % 24;
        var dia = (valor / 86400) % 31 + 1;
        var mes = (valor / (86400 * 31)) % 12 + 1;
        var anio = valor / (86400 * 31 * 12) + 2000;
        try
        {
            return new DateTime((int)anio, (int)mes, (int)dia, (int)hora, (int)minuto, (int)segundo);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    // ---------------------------------------------------------------- comandos de alto nivel

    private async Task DeshabilitarAsync(CancellationToken ct) =>
        await EnviarComandoAsync(CmdDisableDevice, Array.Empty<byte>(), ct);

    private async Task HabilitarSilenciosoAsync(CancellationToken ct)
    {
        try { await EnviarComandoAsync(CmdEnableDevice, Array.Empty<byte>(), ct); }
        catch { /* si falla el re-enable, la sesión se cierra igual */ }
    }

    private sealed record Cantidades(int Usuarios, int Marcas);

    /// <summary>CMD_GET_FREE_SIZES: 20 enteros; usuarios=[4], marcas=[8].</summary>
    private async Task<Cantidades> LeerCantidadesAsync(CancellationToken ct)
    {
        var respuesta = await EnviarComandoAsync(CmdGetFreeSizes, Array.Empty<byte>(), ct);
        if (!respuesta.Ok || respuesta.Payload.Length < 80) return new Cantidades(0, 0);
        var usuarios = BinaryPrimitives.ReadInt32LittleEndian(respuesta.Payload.AsSpan(16, 4));
        var marcas = BinaryPrimitives.ReadInt32LittleEndian(respuesta.Payload.AsSpan(32, 4));
        return new Cantidades(usuarios, marcas);
    }

    /// <summary>Lee una opción de configuración (CMD_OPTIONS_RRQ), ej. "~SerialNumber".</summary>
    private async Task<string?> LeerOpcionAsync(string opcion, CancellationToken ct)
    {
        try
        {
            var payload = System.Text.Encoding.ASCII.GetBytes(opcion).Concat(new byte[] { 0 }).ToArray();
            var respuesta = await EnviarComandoAsync(CmdOptionsRrq, payload, ct);
            if (!respuesta.Ok) return null;
            var texto = System.Text.Encoding.ASCII.GetString(respuesta.Payload);
            var igual = texto.IndexOf('=');
            return igual >= 0 ? texto[(igual + 1)..].Split('\0')[0].Trim() : texto.Split('\0')[0].Trim();
        }
        catch
        {
            return null;
        }
    }

        // ---------------------------------------------------------------- capa de transporte

    private sealed record Respuesta(bool Ok, ushort Comando, ushort Sesion, ushort ReplyId, byte[] Payload);

    private async Task<Respuesta> EnviarComandoAsync(ushort comando, byte[] payload, CancellationToken ct)
    {
        var respuesta = await EnviarInternoAsync(comando, payload, ct);
        if (respuesta.Comando == AckError)
            throw new InvalidOperationException($"El reloj rechazó el comando {comando} (ACK_ERROR).");
        return respuesta;
    }

    private async Task<Respuesta> EnviarInternoAsync(ushort comando, byte[] payload, CancellationToken ct)
    {
        // Timeout duro por comando: la lectura async no respeta ReadTimeout y sin esto un equipo que
        // no responde cuelga el request hasta el 504 del proxy.
        using var vencimiento = CancellationTokenSource.CreateLinkedTokenSource(ct);
        vencimiento.CancelAfter(TimeSpan.FromSeconds(8));

        try
        {
            // El checksum se calcula con el reply_id actual (quirk del protocolo); ArmarPaquete lo
            // incrementa solo en el paquete final.
            var interno = ArmarPaquete(comando, _sessionId, _replyId, payload);
            var prefijo = new byte[8];
            BinaryPrimitives.WriteUInt16LittleEndian(prefijo.AsSpan(0, 2), 20560);
            BinaryPrimitives.WriteUInt16LittleEndian(prefijo.AsSpan(2, 2), 32130);
            BinaryPrimitives.WriteUInt32LittleEndian(prefijo.AsSpan(4, 4), (uint)interno.Length);

            await _stream.WriteAsync(prefijo, vencimiento.Token);
            await _stream.WriteAsync(interno, vencimiento.Token);

            var respuesta = await LeerMensajeAsync(vencimiento.Token);
            _sessionId = respuesta.Sesion;
            _replyId = respuesta.ReplyId;
            return respuesta;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new IOException("El reloj no respondió (timeout de 8 segundos). Puede estar en uso por otro software, con la sesión trabada o necesitar un reinicio.");
        }
    }

    /// <summary>
    /// ACK del cliente tras un paquete de datos (idéntico a pyzk __ack_ok): comando CMD_ACK_OK con
    /// reply_id fijo, sin alterar el estado de la sesión. Sin esto el equipo queda esperándolo
    /// indefinidamente y su módulo de comunicación se traba.
    /// </summary>
    private async Task EnviarAckOkClienteAsync(CancellationToken ct)
    {
        using var vencimiento = CancellationTokenSource.CreateLinkedTokenSource(ct);
        vencimiento.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            var interno = ArmarPaquete(AckOk, _sessionId, ushort.MaxValue - 1, Array.Empty<byte>());
            var prefijo = new byte[8];
            BinaryPrimitives.WriteUInt16LittleEndian(prefijo.AsSpan(0, 2), 20560);
            BinaryPrimitives.WriteUInt16LittleEndian(prefijo.AsSpan(2, 2), 32130);
            BinaryPrimitives.WriteUInt32LittleEndian(prefijo.AsSpan(4, 4), (uint)interno.Length);

            await _stream.WriteAsync(prefijo, vencimiento.Token);
            await _stream.WriteAsync(interno, vencimiento.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new IOException("El reloj no aceptó la confirmación de datos (timeout).");
        }
    }

    private async Task<Respuesta> LeerMensajeAsync(CancellationToken ct)
    {
        var prefijo = await LeerExactoAsync(8, ct);
        var magic1 = BinaryPrimitives.ReadUInt16LittleEndian(prefijo.AsSpan(0, 2));
        var magic2 = BinaryPrimitives.ReadUInt16LittleEndian(prefijo.AsSpan(2, 2));
        if (magic1 != 20560 || magic2 != 32130)
            throw new IOException("Respuesta del reloj con formato inválido.");
        var largo = BinaryPrimitives.ReadUInt32LittleEndian(prefijo.AsSpan(4, 4));
        if (largo < 16 || largo > 128 * 1024 * 1024)
            throw new IOException($"Respuesta del reloj con largo inválido ({largo}).");
        var interno = await LeerExactoAsync((int)largo, ct);

        var comando = BinaryPrimitives.ReadUInt16LittleEndian(interno.AsSpan(0, 2));
        var sesion = BinaryPrimitives.ReadUInt16LittleEndian(interno.AsSpan(4, 2));
        var reply = BinaryPrimitives.ReadUInt16LittleEndian(interno.AsSpan(6, 2));
        var payload = interno.Length > 16 ? interno[16..] : Array.Empty<byte>();
        var ok = comando is AckOk or CmdPrepareData or CmdData;
        return new Respuesta(ok, comando, sesion, reply, payload);
    }

    /// <summary>
    /// Paquete interno de 16 bytes + payload: comando(2) + checksum(2) + sesión(2) + reply_id(2).
    /// Quirk del protocolo (vía pyzk): el checksum se calcula con el reply_id ANTES de incrementarlo,
    /// pero el paquete viaja con el reply_id incrementado.
    /// </summary>
    private static byte[] ArmarPaquete(ushort comando, ushort sesion, ushort replyId, byte[] payload)
    {
        var interno = new byte[16 + payload.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(interno.AsSpan(0, 2), comando);
        BinaryPrimitives.WriteUInt16LittleEndian(interno.AsSpan(4, 2), sesion);
        BinaryPrimitives.WriteUInt16LittleEndian(interno.AsSpan(6, 2), replyId);
        payload.CopyTo(interno.AsSpan(16));

        var checksum = CalcularChecksum(interno);

        var replyIdIncrementado = (ushort)(replyId + 1);
        if (replyIdIncrementado >= ushort.MaxValue) replyIdIncrementado -= ushort.MaxValue;
        BinaryPrimitives.WriteUInt16LittleEndian(interno.AsSpan(6, 2), replyIdIncrementado);
        BinaryPrimitives.WriteUInt16LittleEndian(interno.AsSpan(2, 2), checksum);
        return interno;
    }

    /// <summary>Checksum del protocolo ZK (copiado de zkemsdk.c vía pyzk, con sus plegados peculiares).</summary>
    private static ushort CalcularChecksum(byte[] p)
    {
        uint checksum = 0;
        var l = p.Length;
        var i = 0;
        while (l > 1)
        {
            checksum += (uint)(p[i] | (p[i + 1] << 8));
            i += 2;
            l -= 2;
            if (checksum > ushort.MaxValue) checksum -= ushort.MaxValue;
        }
        if (l == 1) checksum += p[i];
        while (checksum > ushort.MaxValue) checksum -= ushort.MaxValue;

        long conComplemento = ~((long)checksum);
        while (conComplemento < 0) conComplemento += ushort.MaxValue;
        return (ushort)conComplemento;
    }

    private async Task<byte[]> LeerExactoAsync(int cantidad, CancellationToken ct)
    {
        var buffer = new byte[cantidad];
        var leido = 0;
        while (leido < cantidad)
        {
            var n = await _stream.ReadAsync(buffer.AsMemory(leido, cantidad - leido), ct);
            if (n == 0) throw new IOException("El reloj cerró la conexión.");
            leido += n;
        }
        return buffer;
    }

    /// <summary>Ofuscación de CommKey para CMD_AUTH (copiado de commpro.c vía pyzk make_commkey).</summary>
    private static byte[] MakeCommKey(int commKey, ushort sesion)
    {
        uint k = 0;
        for (var i = 0; i < 32; i++)
            k = (commKey & (1 << i)) != 0 ? (k << 1) | 1 : k << 1;
        k += sesion;

        var b = BitConverter.GetBytes(k);
        b[0] ^= (byte)'Z';
        b[1] ^= (byte)'K';
        b[2] ^= (byte)'S';
        b[3] ^= (byte)'O';
        (b[0], b[2]) = (b[2], b[0]);
        (b[1], b[3]) = (b[3], b[1]);
        const byte ticks = 50;
        b[0] ^= ticks;
        b[1] ^= ticks;
        b[2] = ticks;
        b[3] ^= ticks;
        return b;
    }

    public async Task DesconectarAsync(CancellationToken ct = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            await EnviarInternoAsync(CmdExit, Array.Empty<byte>(), timeout.Token);
        }
        catch { /* cierre best-effort */ }
        Dispose();
    }

    public void Dispose()
    {
        _stream.Dispose();
        _tcp.Dispose();
        _lock.Dispose();
    }
}
