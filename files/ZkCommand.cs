namespace ZkAttendanceService.Device;

/// <summary>
/// Códigos de comando del protocolo binario estándar de los relojes ZKTeco
/// (el mismo que usan por debajo zkemkeeper y las implementaciones open source
/// del protocolo, como pyzk). Es un protocolo público, ampliamente documentado
/// por la comunidad de integradores de control de acceso/asistencia.
/// </summary>
public static class ZkCommand
{
    public const ushort CMD_CONNECT = 1000;
    public const ushort CMD_EXIT = 1001;
    public const ushort CMD_ENABLEDEVICE = 1002;
    public const ushort CMD_DISABLEDEVICE = 1003;

    public const ushort CMD_ACK_OK = 2000;
    public const ushort CMD_ACK_ERROR = 2001;
    public const ushort CMD_ACK_DATA = 2002;
    public const ushort CMD_ACK_UNAUTH = 2005;

    public const ushort CMD_PREPARE_DATA = 1500;
    public const ushort CMD_DATA = 1501;

    public const ushort CMD_ATTLOG_RRQ = 13;
    public const ushort CMD_AUTH = 1102;
}
