using System.Buffers.Binary;

namespace ZkAttendanceService.Device;

public readonly record struct ZkReply(ushort Command, ushort SessionId, ushort ReplyId, byte[] Payload);

/// <summary>
/// Construcción y parseo de paquetes del protocolo binario ZK, incluyendo el
/// encabezado adicional de 8 bytes que envuelve cada paquete cuando el transporte
/// es TCP (a diferencia de UDP, que no lo usa).
/// </summary>
internal static class ZkPacket
{
    private static readonly byte[] TcpHeaderPrefix = { 0x50, 0x50, 0x82, 0x7D };

    public static byte[] BuildCommand(ushort command, ushort sessionId, ushort replyId, byte[]? payload = null)
    {
        payload ??= Array.Empty<byte>();

        var packet = new byte[8 + payload.Length];
        WriteHeader(packet, command, 0, sessionId, replyId);
        payload.CopyTo(packet, 8);

        var checksum = ComputeChecksum(packet);
        WriteHeader(packet, command, checksum, sessionId, replyId);

        return WrapTcp(packet);
    }

    private static void WriteHeader(byte[] packet, ushort command, ushort checksum, ushort sessionId, ushort replyId)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(0, 2), command);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2, 2), checksum);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(4, 2), sessionId);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(6, 2), replyId);
    }

    private static byte[] WrapTcp(byte[] packet)
    {
        var wrapped = new byte[8 + packet.Length];
        TcpHeaderPrefix.CopyTo(wrapped, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(wrapped.AsSpan(4, 4), (uint)packet.Length);
        packet.CopyTo(wrapped, 8);
        return wrapped;
    }

    /// <summary>
    /// Checksum de 16 bits en complemento a uno usado por el protocolo ZK. Se calcula
    /// sobre el paquete completo (comando + sessionId + replyId + payload) con el
    /// campo de checksum en cero.
    /// </summary>
    public static ushort ComputeChecksum(ReadOnlySpan<byte> data)
    {
        int checksum = 0;
        int i = 0;
        int remaining = data.Length;

        while (remaining > 1)
        {
            int word = data[i] | (data[i + 1] << 8);
            checksum += word;
            if (checksum > ushort.MaxValue) checksum -= ushort.MaxValue;
            i += 2;
            remaining -= 2;
        }

        if (remaining == 1)
        {
            checksum += data[i];
        }

        while (checksum > ushort.MaxValue) checksum -= ushort.MaxValue;

        checksum = ~checksum;
        while (checksum < 0) checksum += ushort.MaxValue;

        return (ushort)checksum;
    }

    public static ZkReply ParseReply(byte[] packet)
    {
        var command = BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(0, 2));
        var sessionId = BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(4, 2));
        var replyId = BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(6, 2));
        var payload = packet.Length > 8 ? packet[8..] : Array.Empty<byte>();
        return new ZkReply(command, sessionId, replyId, payload);
    }
}
