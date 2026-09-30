# ZkAttendanceService

Worker Service .NET 8 que se conecta por TCP a un reloj biométrico ZKTeco 628C
(192.168.1.204:4370), descarga el log de asistencia usando el protocolo binario
estándar ZK (sin depender de `zkemkeeper.dll`), y lo inserta en la base SQL Server
del software del proveedor.

## Ejecutar

```
dotnet restore
dotnet run
```

Configurar `appsettings.json` (o variables de entorno / user-secrets) con:
- `ZkDevice:SqlConnectionString` → cadena de conexión real a la base del proveedor.
- `ZkDevice:CommKey` → solo si el reloj tiene password configurada (dejar `null` si no).

## Dos cosas a validar contra el equipo real antes de producción

1. **Layout del registro de asistencia** (`Device/AttendanceLogParser.cs`): se asume
   el formato de 40 bytes por registro, el más común en relojes standalone ZK
   clásicos. Hacé una marcación de prueba, descargá el log, y usá
   `AttendanceLogParser.DumpRecordHex` para confirmar que el PIN y el timestamp caen
   donde se espera. Si el buffer total no es múltiplo de 40, es señal de que el
   firmware de este 628C puntual usa otro tamaño de registro.

2. **Esquema de destino** (`Data/AttendanceRepository.cs`): se usa como placeholder
   la tabla `dbo.CHECKINOUT` con columnas `USERID/CHECKTIME/VERIFYCODE/SENSORID`
   (esquema típico ZKTime/ZKBio). Reemplazar por el esquema real de la base del
   proveedor — conviene inspeccionarlo antes de asumir que coincide.

## Consideración operativa

El reloj normalmente acepta una sola sesión TCP activa. Por eso cada ciclo del
`Worker` conecta, deshabilita el dispositivo, lee, rehabilita y desconecta — no
mantiene la sesión abierta entre ciclos, para minimizar el choque con el software
oficial del proveedor si también se conecta al mismo equipo.

## Autenticación (CommKey)

El algoritmo de `BuildAuthKey` en `ZkTcpClient` solo se ejercita si el dispositivo
responde `CMD_ACK_UNAUTH` al conectar (o sea, si tiene password configurada). Si tu
628C no tiene comm key seteada, este camino nunca se usa y no hace falta validarlo.
