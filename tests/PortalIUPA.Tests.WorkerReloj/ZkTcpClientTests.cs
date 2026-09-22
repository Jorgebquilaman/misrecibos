using System.Buffers.Binary;
using System.Text;
using PortalIUPA.Worker.Reloj.Device;
using PortalIUPA.Worker.Reloj.Models;
using Xunit;

namespace PortalIUPA.Tests.WorkerReloj;

public class ZkTcpClientTests
{
    /// <summary>Registro TFT de 40 bytes de ejemplo: legajo "382", 22/09/2026 08:12:45, estado 0 (entrada), huella (1).</summary>
    private static byte[] Registro40Ejemplo()
    {
        var r = new byte[40];
        BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(0, 2), 1);
        Encoding.ASCII.GetBytes("382").CopyTo(r, 2);                                                 // user_id (24)
        r[26] = 0;                                                            // estado: entrada
        Codificar(2026, 9, 22, 8, 12, 45).CopyTo(r, 27);                      // tiempo
        r[31] = 1;                                                            // punch: huella
        return r;
    }

    private static byte[] Registro40Salida()
    {
        var r = new byte[40];
        BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(0, 2), 2);
        Encoding.ASCII.GetBytes("1061").CopyTo(r, 2);
        r[26] = 1;                                                            // estado: salida
        Codificar(2026, 9, 22, 13, 5, 2).CopyTo(r, 27);
        r[31] = 1;
        return r;
    }

    private static byte[] Codificar(int anio, int mes, int dia, int hora, int minuto, int segundo)
    {
        var valor = segundo + minuto * 60 + hora * 3600 + (dia - 1) * 86400
            + (mes - 1) * 2678400 + (anio - 2000) * 32140800;
        var b = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, (uint)valor);
        return b;
    }

    [Fact]
    public void ParseRegistro40_ExtraeLegajoFechaEstadoYVerificacion()
    {
        var registro = ZkTcpClient.ParseRegistro40(Registro40Ejemplo(), 0);

        Assert.NotNull(registro);
        Assert.Equal("382", registro!.Pin);
        Assert.Equal(new DateTime(2026, 9, 22, 8, 12, 45), registro.Timestamp);
        Assert.Equal(0, registro.Estado);
        Assert.False(registro.EsSalida);
        Assert.Equal(1, registro.ModoVerificacion); // huella
    }

    [Fact]
    public void ParseRegistro40_DetectaSalida()
    {
        var registro = ZkTcpClient.ParseRegistro40(Registro40Salida(), 0);

        Assert.Equal("1061", registro!.Pin);
        Assert.Equal(new DateTime(2026, 9, 22, 13, 5, 2), registro.Timestamp);
        Assert.True(registro.EsSalida);
    }

    [Fact]
    public void InferirTamanioRegistro_Devuelve40CuandoElBloqueEsMultiplo()
    {
        var bloque = new byte[80]; // 2 registros de 40
        Assert.Equal(40, ZkTcpClient.InferirTamanioRegistro(bloque));
        Assert.Equal(16, ZkTcpClient.InferirTamanioRegistro(new byte[32]));
        Assert.Equal(8, ZkTcpClient.InferirTamanioRegistro(new byte[24]));
    }

    [Fact]
    public void ArmarPaquete_EscribeComandoSesionYChecksumConQuirkDeReplyId()
    {
        var payload = new byte[] { 0x01, 0x02, 0x03 };
        var paquete = ZkTcpClient.ArmarPaquete(1000, sesion: 0, replyId: 65534, payload);

        // Encabezado interno: comando, checksum, sesión, reply_id incrementado
        var comando = BinaryPrimitives.ReadUInt16LittleEndian(paquete.AsSpan(0, 2));
        var sesion = BinaryPrimitives.ReadUInt16LittleEndian(paquete.AsSpan(4, 2));
        var replyId = BinaryPrimitives.ReadUInt16LittleEndian(paquete.AsSpan(6, 2));

        Assert.Equal((ushort)1000, comando);
        Assert.Equal((ushort)0, sesion);
        Assert.Equal((ushort)0, replyId); // quirk: incrementa y 65535 se pliega a 0

        // El checksum en el paquete coincide con el calculado sobre el paquete con reply_id sin incrementar.
        var verificado = new byte[paquete.Length];
        paquete.CopyTo(verificado, 0);
        var chkEnPaquete = BinaryPrimitives.ReadUInt16LittleEndian(verificado.AsSpan(2, 2));
        BinaryPrimitives.WriteUInt16LittleEndian(verificado.AsSpan(2, 2), 0);      // limpiar el campo checksum
        BinaryPrimitives.WriteUInt16LittleEndian(verificado.AsSpan(6, 2), 65534); // reply_id sin incrementar
        var chkEsperado = ZkTcpClient.CalcularChecksum(verificado);
        Assert.Equal(chkEsperado, chkEnPaquete);
    }

    [Fact]
    public void CodificarYDecodificarTiempoZk_EsIdempotente()
    {
        var fecha = new DateTime(2026, 9, 22, 14, 33, 21);
        var bytes = ZkTcpClient.CodificarTiempoZk(fecha);
        var valor = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        Assert.Equal(fecha, ZkTcpClient.DecodificarTiempoZk(valor));
    }

    [Fact]
    public void DecodificarTiempoZk_FechaMinValueParaValoresInvalidos()
    {
        // Un valor que produce 31/2 (febrero 31) → inválido → DateTime.MinValue
        var anio2026 = (2026 - 2000) * (86400 * 31 * 12);
        var mes2 = (2 - 1) * 2678400;
        var dia31 = (31 - 1) * 86400;
        var valor = (uint)(anio2026 + mes2 + dia31 + 3600); // 31 de febrero
        Assert.Equal(DateTime.MinValue, ZkTcpClient.DecodificarTiempoZk(valor));
    }
}
