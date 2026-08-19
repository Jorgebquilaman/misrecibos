# Portal del Empleado IUPA

Reemplazo del sistema legacy *MisRecibos* (ASP.NET Web Forms + SQL Server + JasperReports) por una
arquitectura moderna: **API .NET 8 (arquitectura hexagonal) + PostgreSQL + SPA React (PWA)**.

## Módulos

| Módulo | Estado |
|---|---|
| Recibos de sueldo (JasperReports Server) | v1 |
| Licencias (workflow multinivel + adjuntos + límites) | v1 |
| Fichadas (sync ETL del checador SQL Server) | v1 |
| Anuncios | v1 |
| Notificaciones in-app + email | v1 |
| Certificados laborales (PDF generado en QuestPDF) | v1 |
| Login Google (`@iupa.edu.ar`) + JWT + roles | v1 |
| ABMs (empleados, áreas, períodos, tipos de licencia, relaciones) | v1 |
| Estadísticas de accesos | v1 |
| Directorio, Feriados, Legajo digital, Capacitaciones, Encuestas, Novedades liquidación, HelpDesk | roadmap |

## Arquitectura

```
src/
  PortalIUPA.Domain         Entidades, value objects, enums, puertos (interfaces) y servicios de dominio
  PortalIUPA.Application    Use cases CQRS (MediatR), validación (FluentValidation), DTOs
  PortalIUPA.Infrastructure EF Core + Npgsql, repositorios, Jasper, SMTP (MailKit), QuestPDF, JWT, sync reloj
  PortalIUPA.Api            Controllers, autenticación Google + JWT, middleware de errores, seed
tests/
  PortalIUPA.Domain.Tests   40 pruebas
  PortalIUPA.Application.Tests  16 pruebas
frontend/                   React 18 + Vite + TypeScript + Tailwind + zustand + PWA
```

Reglas de dominio clave:
- Límites mensual/anual de licencias se validan contra consumo (aprobadas + en espera).
- Rechazo en cualquier nivel corta la cadena; tipo sin niveles queda auto-aprobada.
- El primer nivel se resuelve por `RelacionACargo` vigente (responsable directo); el resto por rol.
- El checador (SQL Server legacy o mock) se copia a Postgres con un job periódico; el dominio nunca se acopla al origen.

## Requisitos

- .NET 8 SDK, Docker (PostgreSQL 16), Node 22.

## Puesta en marcha (desarrollo)

1. Base de datos:

   ```bash
   docker compose up -d db
   ```

2. API (aplica migraciones y siembra datos de desarrollo automáticamente):

   ```bash
   dotnet run --project src/PortalIUPA.Api --urls http://localhost:5001
   ```

3. Frontend:

   ```bash
   cd frontend && npm install && npm run dev   # http://localhost:5173
   ```

4. Tests:

   ```bash
   dotnet test PortalIUPA.slnx
   ```

### Login de desarrollo

Sin credenciales de Google (sección `Google` vacía) el botón de Google no se registra. Para probar,
en `appsettings.Development.json` `Auth:DevLoginEnabled=true` habilita el selector de usuarios de
prueba en la pantalla de login. Usuarios sembrados:

| Correo | Rol |
|---|---|
| `ana.garcia@iupa.edu.ar`, `carlos.perez@iupa.edu.ar`, `maria.torres@iupa.edu.ar`, `juan.diaz@iupa.edu.ar` | Empleado |
| `martin.lopez@iupa.edu.ar` | Responsable |
| `silvia.ramos@iupa.edu.ar` | Dirección |
| `rrhh@iupa.edu.ar` | RRHH |
| `administrador@iupa.edu.ar` | Administrador |

### Google OAuth

1. Crear credenciales OAuth en Google Cloud Console (tipo "Aplicación web") con
   `http://localhost:5001/signin-google` como URI de redirección autorizada.
2. Configurar `Google:ClientId` y `Google:ClientSecret` (en `appsettings.Development.json` o variables de entorno).
3. El callback valida el dominio `@iupa.edu.ar` y redirige al frontend con el JWT en el fragmento
   (`/auth/callback#token=...`).

### Recibos (JasperReports Server)

El reporte legacy se consume por REST (`/rest_v2/reports/Mapuche/Reportes/Recibos_de_Sueldo_simple.pdf`)
con los parámetros `nroliq`, `nroleg_f`, `nroleg_i` (= legajo). Configurar sección `Jasper`
(BaseUrl, Usuario, Contrasena). Sin servidor Jasper, los recibos no están disponibles.

### Reloj checador

- `Reloj:Fuente=Mock` (por defecto): genera marcas deterministas de los últimos 30 días para los legajos indicados.
- `Reloj:Fuente=SqlServer`: lee la vista `Reloj:Vista` (default `Vista_MarcasReloj`) de la base legacy
  con `Reloj:SqlServerConnectionString`.
- El job `SincronizadorRelojService` corre cada `Reloj:Sincronizacion:IntervaloMinutos` (default 15).

### SMTP

`Smtp:*` (MailKit). El envío de emails es *best-effort*: si falla se loguea y no bloquea el flujo de negocio.

## Producción

```bash
docker compose up -d --build
```

Variables requeridas en el entorno: `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`, `JWT_SECRET`
(secreto ≥ 32 caracteres), y opcionalmente `ConnectionStrings__PortalIUPA`, `Reloj__Fuente` y `Smtp__*`.

## Desvíos documentados

- `Empleado.Roles` se mapea como `integer[]` (colección primitiva de enums) en vez de tabla puente:
  simple y suficiente para el rol por empleado.
- Los `DateTime` del dominio se persisten como `timestamp without time zone` (como la base legacy),
  normalizando el `Kind` en la configuración de EF.
- Certificados: PDF generado con QuestPDF (licencia Community); la constancia se emite "en General Roca" (configurable a futuro).
- `Auth:DevLoginEnabled` solo debe activarse en desarrollo.