using Application.Auth;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace Usuarios.Infrastructure.Email;

public class GmailEmailService(
    IConfiguration configuration) : IEmailService
{
    public async Task SendPasswordResetEmailAsync(
        string email,
        string resetLink,
        CancellationToken cancellationToken = default)
    {
        var host = configuration["Smtp:Host"]
            ?? throw new InvalidOperationException("Falta Smtp:Host");

        var port = int.Parse(configuration["Smtp:Port"] ?? "587");

        var username = configuration["Smtp:Username"]
            ?? throw new InvalidOperationException("Falta Smtp:Username");

        var password = configuration["Smtp:Password"]
            ?? throw new InvalidOperationException("Falta Smtp:Password");

        var message = new MimeMessage();

        message.From.Add(
            new MailboxAddress("Portal de Empleos", username));

        message.To.Add(MailboxAddress.Parse(email));

        message.Subject = "Recuperación de contraseña - Portal de Empleos";

        message.Body = new TextPart("plain")
        {
            Text =
                "Recibimos una solicitud para restablecer tu contraseña.\n\n" +
                "Abrí el siguiente enlace:\n" +
                resetLink + "\n\n" +
                "Este enlace vence en 15 minutos.\n\n" +
                "Si no solicitaste el cambio, ignorá este correo."
        };

        using var client = new MailKit.Net.Smtp.SmtpClient();

        await client.ConnectAsync(
            host,
            port,
            SecureSocketOptions.StartTls,
            cancellationToken);

        await client.AuthenticateAsync(
            username,
            password,
            cancellationToken);

        await client.SendAsync(message, cancellationToken);

        await client.DisconnectAsync(true, cancellationToken);
    }
}
