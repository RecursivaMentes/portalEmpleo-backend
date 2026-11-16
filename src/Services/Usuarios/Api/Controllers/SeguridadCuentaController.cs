using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Usuarios.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class SeguridadCuentaController(
    IUsuarioRepository usuarios, IPasswordService claves,
    IConfiguration configuracion, ILogger<SeguridadCuentaController> logger) : ControllerBase
{
    public sealed record CambiarClaveRequest(string? ContrasenaActual, string? NuevaContrasena, string? ConfirmarContrasena);
    public sealed record SolicitarRecuperacionRequest(string? Email);
    public sealed record RestablecerClaveRequest(string? Email, string? Token, string? NuevaContrasena, string? ConfirmarContrasena);

    private static bool ClaveValida(string? clave) => clave is { Length: >= 12 and <= 128 };

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> Cambiar([FromBody] CambiarClaveRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var id))
            return Unauthorized(new { message = "La sesión no es válida." });
        if (!ClaveValida(request.NuevaContrasena) || request.NuevaContrasena != request.ConfirmarContrasena)
            return BadRequest(new { message = "La contraseña nueva debe tener entre 12 y 128 caracteres y coincidir con su confirmación." });
        var usuario = await usuarios.GetByIdAsync(id, ct);
        if (usuario is null || !usuario.Estado) return Unauthorized(new { message = "La cuenta no está disponible." });
        if (string.IsNullOrWhiteSpace(request.ContrasenaActual) || !claves.VerifyPassword(usuario.PasswordHash, request.ContrasenaActual))
            return BadRequest(new { message = "La contraseña actual es incorrecta." });
        if (claves.VerifyPassword(usuario.PasswordHash, request.NuevaContrasena!))
            return BadRequest(new { message = "La contraseña nueva debe ser diferente de la actual." });
        usuario.PasswordHash = claves.HashPassword(request.NuevaContrasena!);
        await usuarios.UpdateAsync(usuario, ct);
        return Ok(new { message = "Contraseña actualizada correctamente." });
    }

    private byte[]? ClaveFirma()
    {
        var texto = configuracion["PasswordReset:Key"];
        if (string.IsNullOrWhiteSpace(texto)) return null;
        try { var clave = Convert.FromBase64String(texto); return clave.Length >= 32 ? clave : null; }
        catch (FormatException) { return null; }
    }

    private static byte[] Mac(byte[] clave, string hash, string payload)
    {
        var derivada = HMACSHA256.HashData(clave, Encoding.UTF8.GetBytes(hash));
        return HMACSHA256.HashData(derivada, Encoding.UTF8.GetBytes(payload));
    }

    private string GenerarToken(Guid id, string hash, byte[] clave)
    {
        var payload = $"{id:N}.{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.{WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(24))}";
        return $"{payload}.{WebEncoders.Base64UrlEncode(Mac(clave, hash, payload))}";
    }

    private static bool ValidarToken(Guid usuarioId, string hash, byte[] clave, string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 1000) return false;
        var partes = token.Split('.');
        if (partes.Length != 4 || partes[0] != usuarioId.ToString("N") || !long.TryParse(partes[1], out var segundos)) return false;
        if (segundos < 0 || segundos > 253402300799L) return false;
        var emitido = DateTimeOffset.FromUnixTimeSeconds(segundos);
        var antiguedad = DateTimeOffset.UtcNow - emitido;
        if (antiguedad < TimeSpan.Zero || antiguedad > TimeSpan.FromMinutes(20)) return false;
        try
        {
            var firmaRecibida = WebEncoders.Base64UrlDecode(partes[3]);
            var payload = string.Join('.', partes.Take(3));
            var firmaEsperada = Mac(clave, hash, payload);
            return firmaRecibida.Length == firmaEsperada.Length && CryptographicOperations.FixedTimeEquals(firmaRecibida, firmaEsperada);
        }
        catch (FormatException) { return false; }
    }

    private bool EmailConfigurado() =>
        !string.IsNullOrWhiteSpace(configuracion["Email:SmtpHost"]) &&
        !string.IsNullOrWhiteSpace(configuracion["Email:From"]) &&
        !string.IsNullOrWhiteSpace(configuracion["Frontend:BaseUrl"]);

    [HttpPost("forgot-password")]
    public async Task<IActionResult> Solicitar([FromBody] SolicitarRecuperacionRequest request, CancellationToken ct)
    {
        var firma = ClaveFirma();
        if (firma is null || !EmailConfigurado())
            return StatusCode(503, new { message = "La recuperación por correo todavía no está configurada en el servidor." });
        if (string.IsNullOrWhiteSpace(request.Email) || !new EmailAddressAttribute().IsValid(request.Email))
            return BadRequest(new { message = "Ingresá un correo electrónico válido." });
        var usuario = await usuarios.GetByEmailAsync(request.Email.Trim(), ct);
        if (usuario is { Estado: true })
        {
            var token = GenerarToken(usuario.Id, usuario.PasswordHash, firma);
            var web = configuracion["Frontend:BaseUrl"]!.TrimEnd('/');
            var enlace = $"{web}/recuperar-clave?email={Uri.EscapeDataString(usuario.Email)}&token={Uri.EscapeDataString(token)}";
            try
            {
                using var mensaje = new MailMessage(configuracion["Email:From"]!, usuario.Email)
                {
                    Subject = "Recuperación de contraseña - RecursivaMentes",
                    Body = $"Solicitaste recuperar el acceso a tu cuenta. Usá este enlace (válido durante 20 minutos):\n\n{enlace}\n\nSi no lo solicitaste, ignorá este mensaje.",
                    IsBodyHtml = false
                };
                using var smtp = new SmtpClient(configuracion["Email:SmtpHost"], int.TryParse(configuracion["Email:SmtpPort"], out var port) ? port : 587)
                { EnableSsl = true, DeliveryMethod = SmtpDeliveryMethod.Network };
                var usuarioSmtp = configuracion["Email:Username"];
                var claveSmtp = configuracion["Email:Password"];
                if (!string.IsNullOrWhiteSpace(usuarioSmtp) && !string.IsNullOrWhiteSpace(claveSmtp))
                    smtp.Credentials = new NetworkCredential(usuarioSmtp, claveSmtp);
                await smtp.SendMailAsync(mensaje, ct);
            }
            catch (Exception ex) when (ex is SmtpException or InvalidOperationException)
            {
                logger.LogError(ex, "No se pudo enviar correo de recuperación.");
                return StatusCode(503, new { message = "El servicio de correo no está disponible. Intentá nuevamente más tarde." });
            }
        }
        // Respuesta uniforme: no permite averiguar si una dirección está registrada.
        return Ok(new { message = "Si la cuenta existe, enviamos un enlace de recuperación." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> Restablecer([FromBody] RestablecerClaveRequest request, CancellationToken ct)
    {
        var firma = ClaveFirma();
        if (firma is null) return StatusCode(503, new { message = "La recuperación no está disponible." });
        if (!ClaveValida(request.NuevaContrasena) || request.NuevaContrasena != request.ConfirmarContrasena)
            return BadRequest(new { message = "La contraseña debe tener entre 12 y 128 caracteres y coincidir con su confirmación." });
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new { message = "El enlace de recuperación es inválido." });
        var usuario = await usuarios.GetByEmailAsync(request.Email.Trim(), ct);
        if (usuario is null || !usuario.Estado || !ValidarToken(usuario.Id, usuario.PasswordHash, firma, request.Token))
            return BadRequest(new { message = "El enlace de recuperación no es válido o venció. Solicitá uno nuevo." });
        usuario.PasswordHash = claves.HashPassword(request.NuevaContrasena!);
        await usuarios.UpdateAsync(usuario, ct);
        return Ok(new { message = "Contraseña actualizada correctamente." });
    }
}
