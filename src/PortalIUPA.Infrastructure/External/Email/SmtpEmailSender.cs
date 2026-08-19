using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using PortalIUPA.Domain.Ports;

namespace PortalIUPA.Infrastructure.External.Email;

public sealed class SmtpOptions
{
    public string Host { get; set; } = "smtp.office365.com";
    public int Puerto { get; set; } = 587;
    public bool UsarSsl { get; set; } = true;
    public string Usuario { get; set; } = "";
    public string Contrasena { get; set; } = "";
    public string Desde { get; set; } = "portal@iupa.edu.ar";
    public string DesdeNombre { get; set; } = "Portal del Empleado IUPA";
}

/// <summary>Envío de correos institucionales vía SMTP (MailKit). Credenciales desde configuración segura.</summary>
public sealed class SmtpEmailSender : IEmailPort
{
    private readonly SmtpOptions _options;

    public SmtpEmailSender(IOptions<SmtpOptions> options) => _options = options.Value;

    public async Task EnviarAsync(string para, string asunto, string cuerpoHtml,
        IReadOnlyList<EmailAdjunto>? adjuntos = null, CancellationToken ct = default)
    {
        var mensaje = new MimeMessage();
        mensaje.From.Add(new MailboxAddress(_options.DesdeNombre, _options.Desde));
        mensaje.To.Add(MailboxAddress.Parse(para));
        mensaje.Subject = asunto;

        var cuerpo = new BodyBuilder { HtmlBody = cuerpoHtml };
        if (adjuntos is not null)
        {
            foreach (var adjunto in adjuntos)
                cuerpo.Attachments.Add(adjunto.NombreArchivo, adjunto.Contenido, ContentType.Parse(adjunto.ContentType));
        }

        mensaje.Body = cuerpo.ToMessageBody();

        using var cliente = new SmtpClient();
        cliente.CheckCertificateRevocation = false;
        await cliente.ConnectAsync(_options.Host, _options.Puerto, _options.UsarSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None, ct);
        if (!string.IsNullOrWhiteSpace(_options.Usuario))
            await cliente.AuthenticateAsync(_options.Usuario, _options.Contrasena, ct);
        await cliente.SendAsync(mensaje, ct);
        await cliente.DisconnectAsync(true, ct);
    }
}