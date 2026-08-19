# Instalación paso a paso

Esta guía explica cómo poner el **Portal del Empleado IUPA** en funcionamiento, desde
cero hasta publicarlo en un servidor con nginx. Está pensada para que la siga cualquier
persona con conocimientos básicos de Linux.

---

## 1. Requisitos

| Componente | Versión | Para qué |
|---|---|---|
| .NET SDK | 8.0 o superior | Compilar y ejecutar la API |
| Node.js | 20 o superior | Compilar la interfaz web (frontend) |
| PostgreSQL | 14 o superior | Base de datos |
| nginx | Cualquiera reciente | Publicar la web (opcional, solo para servidor) |

También vas a necesitar un correo de Google con dominio `@iupa.edu.ar` (para el ingreso)
y las credenciales OAuth de Google (ver paso 6).

---

## 2. Obtener el código

```bash
git clone https://github.com/Jorgebquilaman/misrecibos.git
cd misrecibos
```

El proyecto tiene esta estructura:

```
src/
  PortalIUPA.Domain       Reglas de negocio
  PortalIUPA.Application  Lógica de la aplicación (CQRS)
  PortalIUPA.Infrastructure  Base de datos, emails, PDFs, reloj
  PortalIUPA.Api          API web (punto de entrada)
frontend/                 Interfaz web (React)
tests/                    Pruebas automáticas
```

---

## 3. Crear la base de datos

Con PostgreSQL instalado y corriendo, creá una base llamada `portal_iupa`:

```bash
sudo -u postgres psql -c "CREATE DATABASE portal_iupa;"
```

> Las tablas se crean solas: la API aplica las migraciones automáticamente al
> arrancar por primera vez.

---

## 4. Configurar la API

El archivo de configuración es `src/PortalIUPA.Api/appsettings.json`. Las secciones
importantes:

| Sección | Qué configurás |
|---|---|
| `ConnectionStrings:PortalIUPA` | Dónde está la base de datos (usuario, contraseña, host) |
| `Jwt:Secret` | Clave secreta para firmar los accesos. **Cambiala por una larga y aleatoria** |
| `Google:ClientId` / `ClientSecret` | Credenciales OAuth de Google (ver paso 6) |
| `Frontend:BaseUrl` | URL pública del portal (ej. `https://172.16.0.24:8095`) |
| `Jasper:BaseUrl` | Dirección del servidor JasperReports (recibos) |
| `Smtp` | Servidor de correo para enviar notificaciones y recibos |
| `Reloj:Fuente` | `Mock` para pruebas o `SqlServer` si hay checador de fichadas |
| `Auth:DevLoginEnabled` | `true` solo en desarrollo (entrar sin Google) |

> ⚠️ **Seguridad**: los archivos `appsettings.Development.json` y
> `appsettings.Production.json` contienen credenciales reales y **no se suben al
> repositorio** (están en `.gitignore`). Nunca pongas contraseñas reales en el
> `appsettings.json` que se comparte por git.

---

## 5. Compilar y ejecutar

### En desarrollo (local)

1. API (desde la raíz del proyecto):

   ```bash
   dotnet run --project src/PortalIUPA.Api
   ```

   La API queda en `http://localhost:5001` (documentación en `/swagger` en modo desarrollo).

2. Frontend:

   ```bash
   cd frontend
   npm install
   npm run dev
   ```

   La web queda en `http://localhost:5173` y redirige las llamadas `/api` a la API.

3. (Opcional) Base de datos con Docker en lugar de Postgres local:

   ```bash
   docker compose up -d
   ```

### Para producción (servidor)

1. Publicar la API:

   ```bash
   dotnet publish src/PortalIUPA.Api -c Release -o publish/api
   ```

2. Compilar el frontend:

   ```bash
   cd frontend
   npm install
   npm run build
   ```

   El resultado queda en `frontend/dist/`.

3. Probar las pruebas automáticas (opcional pero recomendado):

   ```bash
   dotnet test
   ```

---

## 6. Configurar el ingreso con Google

1. En [Google Cloud Console](https://console.cloud.google.com) creá una credencial de
   tipo **OAuth Client ID** (tipo *Web application*).
2. Copiá el `ClientId` y el `ClientSecret` a la configuración de la API.
3. En **Authorized redirect URIs** agregá la dirección del callback de tu instalación:

   ```
   https://TU_URL_PUBLICA/api/auth/google/callback
   ```

   > Ejemplo real: `https://172.16.0.24:8095/api/auth/google/callback`.
   > Si publicás también por HTTP, agregá la versión HTTP.

4. El sistema solo deja entrar correos con dominio `@iupa.edu.ar`.

---

## 7. Publicar en un servidor (ejemplo con nginx)

Este es el esquema que se usa en el servidor real (`172.16.0.24`):

| Servicio | Puerto | Detalle |
|---|---|---|
| API | 5003 | Sistema `portal-iupa-api.service` |
| Web HTTP | 8094 | nginx sirve la web y el `/api` |
| Web HTTPS | 8095 | Igual, con certificado SSL |

### 7.1 Copiar los archivos

```bash
sudo mkdir -p /opt/portal-iupa/api /opt/portal-iupa/frontend
sudo cp -r publish/api/* /opt/portal-iupa/api/
sudo cp -r frontend/dist/* /opt/portal-iupa/frontend/
```

### 7.2 Servicio systemd

Creá `/etc/systemd/system/portal-iupa-api.service`:

```ini
[Unit]
Description=Portal IUPA API
After=network.target postgresql.service

[Service]
Type=simple
User=soporte
WorkingDirectory=/opt/portal-iupa/api
ExecStart=/usr/local/bin/dotnet /opt/portal-iupa/api/PortalIUPA.Api.dll
Restart=on-failure
RestartSec=5
Environment=ASPNETCORE_URLS=http://*:5003
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_ROLL_FORWARD=LatestMajor

[Install]
WantedBy=multi-user.target
```

Activá y verificá:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now portal-iupa-api.service
sudo systemctl status portal-iupa-api.service
curl http://127.0.0.1:5003/healthz
```

Si responde `{"estado":"ok"}`, la API está funcionando.

### 7.3 nginx

Creá un archivo en `/etc/nginx/sites-available/portal` (y un enlace en
`sites-enabled`) con esta configuración de ejemplo:

```nginx
server {
    listen 8095 ssl;
    http2 on;
    server_name _;

    ssl_certificate /etc/nginx/ssl/fullchain.pem;
    ssl_certificate_key /etc/nginx/ssl/privkey.pem;
    client_max_body_size 20M;

    location /api/ {
        proxy_pass http://127.0.0.1:5003;
        proxy_http_version 1.1;
        proxy_set_header Host $http_host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto https;
    }

    location / {
        root /opt/portal-iupa/frontend;
        index index.html;
        try_files $uri $uri/ /index.html;
    }
}
```

> Importante: usar `Host $http_host` (con puerto) para que Google redirija al
> callback con la URL correcta.

Luego:

```bash
sudo nginx -t
sudo systemctl reload nginx
```

---

## 8. Verificación final

1. Abrí la URL del portal en el navegador: debería aparecer la pantalla de ingreso.
2. Probá entrar con un correo `@iupa.edu.ar`.
3. Verificá los logs de la API si algo falla:

   ```bash
   tail -f /opt/portal-iupa/api/logs/portal-iupa-*.log
   ```

---

## Solución de problemas frecuentes

| Problema | Causa probable | Solución |
|---|---|---|
| No puedo entrar con Google | Redirect URI no registrada | Agregala en Google Cloud Console (paso 6) |
| Error de conexión a la base | Credenciales mal | Revisá `ConnectionStrings` en la configuración |
| No se ven fichadas del reloj | Checador inalcanzable | Usá `Reloj:Fuente=Mock` o habilitá la red al checador |
| No llegan correos | Contraseña SMTP inválida | Usá una App Password de Gmail para la cuenta de envío |

---

¿Ya está instalado? Ahora te conviene leer las guías de uso:

- [Uso como Empleado](uso-empleado.md)
- [Uso como Responsable](uso-responsable.md)
- [Uso como RRHH](uso-rrhh.md)
- [Uso como Administrador](uso-administrador.md)