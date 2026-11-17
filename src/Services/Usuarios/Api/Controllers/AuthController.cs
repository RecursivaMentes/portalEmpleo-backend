using System.Text.Json;
using Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Usuarios.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var response = await authService.LoginAsync(
            request, cancellationToken);

        if (response is null)
        {
            return Unauthorized(new
            {
                message = "Email, contraseña incorrectos o usuario inactivo."
            });
        }

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true, // Protege el token contra ataques XSS
            Secure = false,   // permite http en desarrollo
            SameSite = SameSiteMode.Lax, // Necesario porque SameSite=None exige Secure=true
            Expires = response.ExpiresAtUtc
        };

        Response.Cookies.Append("AuthToken", response.AccessToken, cookieOptions);

        return Ok(new { message = "Sesión iniciada correctamente." });
    }

    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        // Debe coincidir con las opciones de SameSite/Secure usadas al crearla
        Response.Cookies.Delete("AuthToken", new CookieOptions
        {
            HttpOnly = true,
            Secure = false,
            SameSite = SameSiteMode.Lax // Igual que en Login
        });

        return Ok(new { message = "Sesión cerrada correctamente." });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var exito = await authService.RegisterAsync(request, cancellationToken);
        if (!exito)
            return Conflict(new { message = "El correo electronico ya esta registrado." });

        return Ok(new { message = "Usuario registrado con exito." });
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userIdToken = User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdToken, out var userId))
            return Unauthorized(new { message = "Token invalido." });

        if (request.Nombre is null &&
            request.Apellido is null &&
            request.Telefono is null &&
            request.PreferenciasJson is null &&
            request.ReglasPostulacion is null)
        {
            return BadRequest(new { message = "Debe indicar al menos un campo para actualizar." });
        }

       
        if (request.PreferenciasJson is not null){
            try
            {   
                using var json = JsonDocument.Parse(request.PreferenciasJson);
                if (json.RootElement.ValueKind != JsonValueKind.Object){
                    ModelState.AddModelError("PreferenciasJson", "PreferenciasJson debe ser un objeto JSON.");
                    return ValidationProblem();
                }
            }
            catch (JsonException){
                ModelState.AddModelError("PreferenciasJson", "PreferenciasJson no contiene JSON válido.");
                return ValidationProblem();
            }
        }
        var exito = await authService.UpdateAsync(userId, request, cancellationToken);
        if (!exito)
            return NotFound(new { message = "Usuario no encontrado." });

        return Ok(new { message = "Perfil actualizado con exito." });
    }

    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var userIdToken = User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdToken, out var userId))
            return Unauthorized(new { message = "Token invalido." });

        var perfil = await authService.GetByIdAsync(userId, cancellationToken);
        if (perfil is null)
            return NotFound(new { message = "Usuario no encontrado." });

        return Ok(perfil);
    }

    [Authorize]
    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new
            {
                message = "Token inválido."
            });
        }

        if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
            string.IsNullOrWhiteSpace(request.NewPassword) ||
            string.IsNullOrWhiteSpace(request.ConfirmPassword))
        {
            return BadRequest(new
            {
                message = "Todos los campos son obligatorios."
            });
        }

        if (request.NewPassword != request.ConfirmPassword)
        {
            return BadRequest(new
            {
                message = "Las contraseñas nuevas no coinciden."
            });
        }

        if (request.NewPassword.Length < 8)
        {
            return BadRequest(new
            {
                message = "La nueva contraseña debe tener al menos 8 caracteres."
            });
        }

        if (request.CurrentPassword == request.NewPassword)
        {
            return BadRequest(new
            {
                message = "La nueva contraseña debe ser diferente a la actual."
            });
        }

        var success = await authService.ChangePasswordAsync(
            userId,
            request,
            cancellationToken);

        if (!success)
        {
            return BadRequest(new
            {
                message = "No se pudo cambiar la contraseña. Verificá la contraseña actual y que la nueva sea diferente."
            });
        }

        return Ok(new
        {
            message = "Contraseña actualizada correctamente."
        });
    }

    [AllowAnonymous]
    [EnableRateLimiting("password-recovery")]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new
            {
                message = "Ingresá tu correo electrónico."
            });

        await authService.ForgotPasswordAsync(
            request,
            cancellationToken);

        return Ok(new
        {
            message = "Si el correo está registrado, recibirás un enlace para restablecer tu contraseña."
        });
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Token) ||
            string.IsNullOrWhiteSpace(request.NewPassword) ||
            string.IsNullOrWhiteSpace(request.ConfirmPassword))
        {
            return BadRequest(new
            {
                message = "Todos los campos son obligatorios."
            });
        }

        if (request.NewPassword != request.ConfirmPassword)
        {
            return BadRequest(new
            {
                message = "Las contraseñas no coinciden."
            });
        }

        if (request.NewPassword.Length < 8)
        {
            return BadRequest(new
            {
                message = "La contraseña debe tener al menos 8 caracteres."
            });
        }

        var success = await authService.ResetPasswordAsync(
            request,
            cancellationToken);

        if (!success)
        {
            return BadRequest(new
            {
                message = "El enlace de recuperación no es válido o ha vencido."
            });
        }

        return Ok(new
        {
            message = "Contraseña restablecida correctamente."
        });
    }
}
