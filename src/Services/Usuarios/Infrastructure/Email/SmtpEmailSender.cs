
using System.Net;
using System.Net.Mail;
using Application.Auth;
using Microsoft.Extensions.Configuration;

namespace Usuarios.Infrastructure.Email;

public class SmtpEmailSender(IConfiguration configuration) : IEmailSender
{
    public async Task SendVerificationCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default)
    {
        var host = configuration["Smtp:Host"]
            ?? throw new InvalidOperationException("Falta Smtp:Host");

        var user = configuration["Smtp:User"]
            ?? throw new InvalidOperationException("Falta Smtp:User");

        var password = configuration["Smtp:Password"]
            ?? throw new InvalidOperationException("Falta Smtp:Password");

        var from = configuration["Smtp:From"] ?? user;

        var port = int.TryParse(configuration["Smtp:Port"], out var configuredPort)
            ? configuredPort
            : 587;

        using var message = new MailMessage(from, email)
        {
            Subject = "Verificacion de cuenta - Portal Empleo",
            Body = $"Tu codigo de verificacion es: {code}\n\nVence en 10 minutos.",
            IsBodyHtml = false
        };

        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(user, password),
            EnableSsl = true
        };

        cancellationToken.ThrowIfCancellationRequested();

        await client.SendMailAsync(message, cancellationToken);
    }
}
