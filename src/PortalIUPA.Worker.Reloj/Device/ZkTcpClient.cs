using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using PortalIUPA.Worker.Reloj.Models;

namespace PortalIUPA.Worker.Reloj.Device;

/// <summary>
/// Resultado de un comando ZK: comando de respuesta, session_id, reply_id y payload.
/// </summary>
public sealed record RespuestaZk(ushort Comando, ushort Sesion, ushort ReplyId, byte[] Payload)
{
    public bool EsOk => Comando is 2000 or 2001 or 1500;
    public bool EsAckOk => Comando == 2000;
}

/// <summary>
/// Cliente TCP propio para el protocolo binario estándar ZK (el mismo que hablan zkemkeeper/pyzk),
/// sin dependencias COM ni de Windows. Implementado contra un ZKTeco X628-C (192.168.1.204:4370).
///
/// Detalles del protocolo que este cliente respeta (extraídos de zkemsdk.c/commpro.c vía pyzk y
/// validados en producción contra este firmware Ver 6.60):
/// - Encabezado externo: 0x50 0x50 0x82 0x7d + largo (uint32 LE) + 2 words de flags.
/// - Encabezado interno de 16 bytes: comando(2) + checksum(2) + session_id(2) + reply_id(2) + 8 reservados.
/// - Checksum 16 bits calculado con el reply_id ANTES de incrementar; el paquete viaja con reply_id + 1.
/// - El cliente usa como reply_id siguiente el que el ECO del dispositivo devuelve.
/// - MDATA: los paquetes de datos largos llegan como CMD_PREPARE_DATA (1500) con [tamaño(4)][datos...]
///   y el resto del bloque llega crudo; o como paquetes CMD_DATA (2001) encadenados. En ambos casos
///   el CLIENTE debe confirmar cada bloque con CMD_ACK_OK o el módulo de comunicación del equipo se traba.
/// - Este firmware se TRABA si se le pide el log con el contador en cero: consultar cantidades
///   (CMD_GET_FREE_SIZES) antes de pedir CMD_ATTLOG_RRQ.
/// - El equipo típicamente acepta UNA sesión TCP a la vez: la sesión es corta (conectar, leer, salir).
/// </summary>
public sealed class ZkTcpClient : IDisposable
{
    public const ushort AckOk = 2000;
    public const ushort AckUnauth = 2000;   // Mismo número: se distingue por contexto.
    public const ushort AckError = 2001 - 2000 + 1999; // (placeholder informativo, no se usa)
    public const ushort CmdData = 2001;
    public const ushort CmdPrepareData = 1500;

    private const ushort CmdConnect = 1000;
    private const ushort CmdExit = 1001;
    private const ushort CmdEnableDevice = 1002;
    private const ushort CmdDisableDevice = 1003;
    private const ushort CmdGetFreeSizes = 50;
    private const ushort CmdRefreshData = 1013;
    private const ushort CmdAuth = 1102;
    private const ushort CmdAttlogRrq = 13;
    private const ushort CmdClearAttlog = 15;

    private readonly TcpClient _tcp;
    private readonly NetworkStream _stream;
    private readonly int _commKey;
    private readonly TimeSpan _timeoutComando;

    private ushort _sessionId;
    private ushort _replyId = unchecked(ushort.MaxValue - 1);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _deshabilitado;

    private ZkTcpClient(TcpClient tcp, int commKey, TimeSpan timeoutComando)
    {
        _tcp = tcp;
        _stream = tcp.GetStream();
        _commKey = commKey;
        _timeoutComando = timeoutComando;
    }

    // ---------------------------------------------------------------- ciclo de sesión

    /// <summary>Conecta, autentica (si hay CommKey) y queda listo para operar. Sesión corta.</summary>
    public static async Task<ZkTcpClient> ConectarAsync(string ip, int puerto, int commKey,
        TimeSpan? timeoutComando = null, CancellationToken ct = default)
    {
        var timeout = timeoutComando ?? TimeSpan.FromSeconds(8);
        var tcp = new TcpClient();
        try
        {
            await tcp.ConnectAsync(ip, puerto, ct);
            if (!tcp.Connected) throw new IOException($"No se pudo conectar a {ip}:{puerto}.");

            var cliente = new ZkTcpClient(tcp, commKey, timeout);
            var respuesta = await cliente.EnviarComandoInternoAsync(CmdConnect, Array.Empty<byte>(), ct);
            if (respuesta.Comando == 2000 && !respuesta.EsOk)
            {
                respuesta = await cliente.EnviarComandoInternoAsync(CmdAuth,
                    MakeCommKey(commKey, respuesta.Sesion), ct);
            }
            if (!respuesta.EsOk)
                throw new IOException($"El reloj rechazó la conexión (código {respuesta.Comando}).");
            return cliente;
        }
        catch
        {
            tcp.Dispose();
            throw;
        }
    }

    /// <summary>Bloquea el equipo para lecturas consistentes. No fatal si el equipo lo rechaza.</summary>
    public async Task<bool> DeshabilitarAsync(CancellationToken ct = default)
    {
        try
        {
            var respuesta = await EnviarComandoInternoAsync(CmdDisableDevice, Array.Empty<byte>(), ct);
            _deshabilitado = respuesta.EsOk;
            return _deshabilitado;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>Rehabilita el equipo (para que vuelva a marcar gente mientras termina el ciclo).</summary>
    public async Task<bool> HabilitarAsync(CancellationToken ct = default)
    {
        if (!_deshabilitado) return true;
        var respuesta = await EnviarComandoInternoAsync(CmdEnableDevice, Array.Empty<byte>(), ct);
        if (respuesta.EsOk) _deshabilitado = false;
        return !_deshabilitado;
    }

    /// <summary>Cantidades (usuarios, marcas) del equipo. Se usa como guard antes de pedir el log.</summary>
    public async Task<(int Usuarios, int Marcas)> LeerCantidadesAsync(CancellationToken ct = default)
    {
        var respuesta = await EnviarComandoInternoAsync(CmdGetFreeSizes, Array.Empty<byte>(), ct);
        if (!respuesta.EsOk || respuesta.Payload.Length < 80) return (0, 0);
        var usuarios = BinaryPrimitives.ReadInt32LittleEndian(respuesta.Payload.AsSpan(16, 4));
        var marcas = BinaryPrimitives.ReadInt32LittleEndian(respuesta.Payload.AsSpan(32, 4));
        return (usuarios, marcas);
    }

    /// <summary>
    /// Lee TODAS las marcas de asistencia del equipo con CMD_ATTLOG_RRQ, reensamblando los
    /// paquetes grandes (MDATA). El llamador debe filtrar por checkpoint; NO se borra el log
    /// del equipo salvo que se llame a VaciarMarcasAsync.
    /// </summary>
    public async Task<IReadOnlyList<AttendanceRecord>> LeerMarcasAsync(CancellationToken ct = default,
        bool borrarAlTerminar = false)
    {
        await _lock.WaitAsync(ct);
        try
        {
            // Guard de contador vacío: este firmware se traba si se le pide el log en cero.
            var (_, cantMarcas) = await LeerCantidadesAsync(ct);
            if (cantMarcas <= 0) return Array.Empty<AttendanceRecord>();

            var datos = await LeerAtlogRawAsync(ct);

            var tamanioRegistro = InferirTamanioRegistro(datos);
            var marcas = new List<AttendanceRecord>();
            if (tamanioRegistro == 0) return marcas;

            var offset = 0;
            while (offset + tamanioRegistro <= datos.Count)
            {
                // Marcador de inicio de registro en algunos firmwares: ff 32 35 35 00 …
                if (offset + 9 <= datos.Count &&
                    datos[offset] == 0xff && datos[offset + 1] == 0x32 && datos[offset + 2] == 0x35 &&
                    datos[offset + 3] == 0x35 && datos[offset + 4] == 0x00)
                {
                    offset += 9;
                    continue;
                }

                var registro = tamanioRegistro switch
                {
                    8 => ParseRegistro8(datos, offset),
                    16 => ParseRegistro16(datos, offset),
                    _ => ParseRegistro40(datos, offset)
                };
                if (registro is not null) marcas.Add(registro);
                offset += tamanioRegistro;
            }

            return marcas;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Borra el log de asistencia del equipo. OPCIONAL y no por defecto: la memoria del
    /// equipo es el respaldo cuando el software del proveedor no captura en tiempo real.</summary>
    public async Task<bool> VaciarMarcasAsync(CancellationToken ct = default)
    {
        await VaciarMarcasInternoAsync(ct);
        return true;
    }

    private async Task VaciarMarcasInternoAsync(CancellationToken ct)
    {
        await EnviarComandoInternoAsync(15 /* CMD_CLEAR_ATTLOG */, Array.Empty<byte>(), ct);
        await EnviarComandoInternoAsync(CmdRefreshData, Array.Empty<byte>(), ct);
    }

    private async Task<List<byte>> LeerAtlogRawAsync(CancellationToken ct)
    {
        var primera = await EnviarComandoInternoAsync(CmdAttlogRrq, Array.Empty<byte>(), ct);
        var datos = new List<byte>();

        if (primera.Comando == CmdPrepareData)
        {
            // Variante de este firmware: bloque grande con [4 bytes de tamaño][primeros datos];
            // el resto llega crudo por el stream. Al terminar, ACK del cliente y cierre.
            var esperado = primera.Payload.Length >= 4
                ? BinaryPrimitives.ReadInt32LittleEndian(primera.Payload.AsSpan(0, 4))
                : 0;
            datos.AddRange(primera.Payload.Length > 4 ? primera.Payload[4..] : Array.Empty<byte>());
            while (datos.Count < esperado)
            {
                var faltan = esperado - datos.Count;
                datos.AddRange(await LeerExactoAsync(faltan, ct));
            }
            await EnviarAckOkClienteAsync(ct);
            // Con log vacío algunos firmwares no envían el ACK final: tolerar timeout corto.
            try
            {
                using var cierre = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cierre.CancelAfter(TimeSpan.FromSeconds(3));
                await LeerMensajeAsync(cierre.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Sin ACK final del equipo: válido, ya tenemos el bloque completo.
            }
        }
        else if (primera.Comando == CmdData)
        {
            // Variante clásica: paquetes CMD_DATA encadenados (reensamblado); tras cada uno
            // el cliente confirma con CMD_ACK_OK. Si el header marca MDATA/continuación,
            // el siguiente paquete llega igualmente como CMD_DATA.
            var comando = primera.Comando;
            var mensaje = primera;
            while (comando == CmdData)
            {
                datos.AddRange(mensaje.Payload);
                await EnviarAckOkClienteAsync(ct);
                var siguiente = await LeerMensajeAsync(ct);
                comando = siguiente.Comando;
                if (comando == AckOk) break;
                if (comando != CmdData)
                    throw new IOException($"Respuesta inesperada durante la lectura de marcas (código {comando}).");
                mensaje = siguiente;
            }
        }
        else if (primera.EsOk)
        {
            // No hay marcas (ACK inicial).
        }
        else
        {
            throw new IOException($"Respuesta inesperada al pedir el log de asistencia (código {primera.Comando}).");
        }

        return datos;
    }

    private async Task EnviarAckOkClienteAsync(CancellationToken ct)
    {
        // ACK del cliente tras un paquete de datos (idéntico a pyzk __ack_ok): sin alterar la sesión,
        // reply_id fijo. Sin esto el módulo de comunicación del equipo se traba indefinidamente.
        var interno = ArmarPaquete(AckOk, _sessionId, ushort.MaxValue - 1, Array.Empty<byte>());
        var prefijo = PrefijoConLargo(interno.Length);
        await _stream.WriteAsync(prefijo, ct);
        await _stream.WriteAsync(interno, ct);
    }

    private async Task<RespuestaZk> EnviarComandoInternoAsync(ushort comando, byte[] payload, CancellationToken ct)
    {
        using var vencimiento = CancellationTokenSource.CreateLinkedTokenSource(ct);
        vencimiento.CancelAfter(_timeoutComando);
        var token = vencimiento.Token;

        // El checksum se calcula con el reply_id actual; el paquete viaja con reply_id + 1.
        var interno = ArmarPaquete(comando, _sessionId, _replyId, payload);
        await _stream.WriteAsync(PrefijoConLargo(interno.Length), token);
        await _stream.WriteAsync(interno, token);

        var respuesta = await LeerMensajeAsync(token);
        _sessionId = respuesta.Sesion;
        _replyId = respuesta.ReplyId;
        return respuesta;
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

    private async Task<RespuestaZk> LeerMensajeAsync(CancellationToken ct)
    {
        var prefijo = await LeerExactoAsync(8, ct);
        if (prefijo[0] != 0x50 || prefijo[1] != 0x50 || prefijo[2] != 0x82 || prefijo[3] != 0x7d)
            throw new IOException("Respuesta del reloj con formato inválido.");
        var largo = BinaryPrimitives.ReadUInt32LittleEndian(prefijo.AsSpan(4, 4));
        if (largo < 16 || largo > 128 * 1024 * 1024)
            throw new IOException($"Respuesta del reloj con largo inválido ({largo}).");
        var interno = await LeerExactoAsync((int)largo, ct);

        var comando = BinaryPrimitives.ReadUInt16LittleEndian(interno.AsSpan(0, 2));
        var sesion = BinaryPrimitives.ReadUInt16LittleEndian(interno.AsSpan(4, 2));
        var reply = BinaryPrimitives.ReadUInt16LittleEndian(interno.AsSpan(6, 2));
        var payload = interno.Length > 16 ? interno[16..] : Array.Empty<byte>();
        return new RespuestaZk(comando, sesion, reply, payload);
    }

    private static byte[] PrefijoConLargo(int largoInterno)
    {
        var prefijo = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(prefijo.AsSpan(0, 2), 20560); // 0x5050
        BinaryPrimitives.WriteUInt16LittleEndian(prefijo.AsSpan(2, 2), 32130); // 0x7d82
        BinaryPrimitives.WriteUInt32LittleEndian(prefijo.AsSpan(4, 4), (uint)largoInterno);
        return prefijo;
    }

    // ---------------- Protocolo (públicos para test unitario) ----------------

    /// <summary>
    /// Paquete interno de 16 bytes + payload: comando(2) + checksum(2) + sesión(2) + reply_id(2) +
    /// 8 reservados + payload. Quirk del protocolo (vía pyzk): el checksum se calcula con el
    /// reply_id ANTES de incrementarlo, pero el paquete viaja con el reply_id incrementado.
    /// </summary>
    public static byte[] ArmarPaquete(ushort comando, ushort sesion, ushort replyId, byte[] payload)
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

    /// <summary>Checksum 16 bits del protocolo ZK (copiado de zkemsdk.c vía pyzk, con sus plegados).</summary>
    public static ushort CalcularChecksum(byte[] p)
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

    /// <summary>Ofuscación de CommKey para CMD_AUTH (copiado de commpro.c vía pyzk make_commkey).</summary>
    public static byte[] MakeCommKey(int commKey, ushort sesion)
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

    /// <summary>Codificación de tiempo ZK: seg + min*60 + hora*3600 + día*86400 + mes*2678400 + (año-2000)*32140800.</summary>
    public static DateTime DecodificarTiempoZk(uint valor)
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

    public static byte[] CodificarTiempoZk(DateTime fecha)
    {
        var valor = (uint)(fecha.Second + fecha.Minute * 60 + fecha.Hour * 3600
            + (fecha.Day - 1) * 86400 + (fecha.Month - 1) * 2678400
            + (fecha.Year - 2000) * 32140800);
        return BitConverter.GetBytes(valor);
    }

    public static int InferirTamanioRegistro(IReadOnlyList<byte> datos)
    {
        if (datos.Count == 0) return 0;
        if (datos.Count % 40 == 0) return 40;
        if (datos.Count % 16 == 0) return 16;
        if (datos.Count % 8 == 0) return 8;
        return 40;
    }

    /// <summary>Registro TFT de 40 bytes: uid(2) + user_id(24) + estado(1)@26 + tiempo(4)@27 + punch(1)@31.</summary>
    public static AttendanceRecord? ParseRegistro40(IReadOnlyList<byte> r, int o)
    {
        var legajo = Encoding.ASCII.GetString(r.Skip(o + 2).Take(24).ToArray()).Split('\0')[0].Trim();
        if (legajo.Length == 0) return null;
        return new AttendanceRecord
        {
            Pin = legajo,
            Timestamp = DecodificarTiempoZk(BinaryPrimitives.ReadUInt32LittleEndian(
                r.Skip(o + 27).Take(4).ToArray())),
            Estado = r[o + 26],
            ModoVerificacion = r[o + 31]
        };
    }

    /// <summary>Registro de 16 bytes: uid(4) + tiempo(4) + estado(1)@8 + punch(1)@9 + reservado(2) + workcode(4).</summary>
    public static AttendanceRecord? ParseRegistro16(IReadOnlyList<byte> r, int o)
    {
        var legajo = BinaryPrimitives.ReadUInt32LittleEndian(r.Skip(o).Take(4).ToArray()).ToString();
        return new AttendanceRecord
        {
            Pin = legajo,
            Timestamp = DecodificarTiempoZk(BinaryPrimitives.ReadUInt32LittleEndian(r.Skip(o + 4).Take(4).ToArray())),
            Estado = r[o + 8],
            ModoVerificacion = r[o + 9]
        };
    }

    /// <summary>Registro compacto de 8 bytes: uid(2) + estado(1)@2 + tiempo(4)@3 + punch(1)@7.</summary>
    public static AttendanceRecord? ParseRegistro8(IReadOnlyList<byte> r, int o)
    {
        var legajo = BinaryPrimitives.ReadUInt16LittleEndian(r.Skip(o).Take(2).ToArray()).ToString();
        return new AttendanceRecord
        {
            Pin = legajo,
            Timestamp = DecodificarTiempoZk(BinaryPrimitives.ReadUInt32LittleEndian(r.Skip(o + 3).Take(4).ToArray())),
            Estado = r[o + 2],
            ModoVerificacion = r[o + 7]
        };
    }

    public async Task SalirAsync(CancellationToken ct = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            if (_deshabilitado)
            {
                try { await HabilitarAsync(timeout.Token); } catch { /* best-effort */ }
            }
            await EnviarComandoInternoAsync(CmdExit, Array.Empty<byte>(), timeout.Token);
        }
        catch { /* cierre best-effort */ }
        finally
        {
            _stream.Dispose();
            _tcp.Dispose();
        }
    }

    public void Dispose()
    {
        _stream.Dispose();
        _tcp.Dispose();
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }
}
