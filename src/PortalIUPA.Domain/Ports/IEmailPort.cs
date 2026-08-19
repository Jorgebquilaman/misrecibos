namespace PortalIUPA.Domain.Ports;

public sealed record EmailAdjunto(string NombreArchivo, byte[] Contenido, string ContentType);

/// <summary>Envío de correos institucionales (SMTP). Usado para recibos por email y notificaciones.</summary>
public interface IEmailPort
{
    Task EnviarAsync(string para, string asunto, string cuerpoHtml, IReadOnlyList<EmailAdjunto>? adjuntos = null,
        CancellationToken ct = default);
}