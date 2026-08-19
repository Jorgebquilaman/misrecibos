using Microsoft.Extensions.Logging;
using PortalIUPA.Domain.Entities;
using PortalIUPA.Domain.Enums;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Application.Services;

/// <summary>
/// Notifica (in-app + email) a aprobadores y empleados ante cambios de estado de licencias.
/// El email es best-effort: si el SMTP falla se registra pero nunca bloquea el flujo de negocio.
/// </summary>
public sealed class NotificadorLicencias
{
    private readonly INotificacionRepository _notificaciones;
    private readonly IEmailPort _email;
    private readonly ILogger<NotificadorLicencias> _logger;

    public NotificadorLicencias(INotificacionRepository notificaciones, IEmailPort email,
        ILogger<NotificadorLicencias> logger)
    {
        _notificaciones = notificaciones;
        _email = email;
        _logger = logger;
    }

    public async Task AvisarAprobadoresAsync(IEnumerable<(Empleado Aprobador, string TipoLicencia, string Solicitante,
            DateOnly Inicio, DateOnly Fin)> avisos, CancellationToken ct)
    {
        foreach (var (aprobador, tipo, solicitante, inicio, fin) in avisos)
        {
            var cuerpo = $"El empleado <b>{solicitante}</b> solicitó una licencia de tipo <b>{tipo}</b> " +
                         $"del {inicio:dd/MM/yyyy} al {fin:dd/MM/yyyy}.";
            await _notificaciones.AddAsync(new Notificacion(aprobador.Id, TipoNotificacion.Licencia,
                "Licencia pendiente de aprobación", cuerpo, "/licencias"), ct);
            await EnviarEmailBestEffortAsync(aprobador.Correo.Valor, "IUPA – Licencia pendiente de aprobación",
                cuerpo, ct);
        }
    }

    public async Task AvisarEmpleadoAsync(Empleado empleado, string titulo, string cuerpo, string? link,
        CancellationToken ct)
    {
        await _notificaciones.AddAsync(new Notificacion(empleado.Id, TipoNotificacion.Licencia, titulo, cuerpo, link), ct);
        await EnviarEmailBestEffortAsync(empleado.Correo.Valor, titulo, cuerpo, ct);
    }

    private async Task EnviarEmailBestEffortAsync(string destinatario, string asunto, string cuerpo,
        CancellationToken ct)
    {
        try
        {
            await _email.EnviarAsync(destinatario, asunto, cuerpo, ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo enviar el email a {Destinatario}.", destinatario);
        }
    }
}