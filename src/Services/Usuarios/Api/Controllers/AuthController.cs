using System.Text.Json;
using Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
            Secure = true,   // Requiere HTTPS
            SameSite = SameSiteMode.Strict, // Cambia a SameSiteMode.None si el front está en otro dominio/puerto
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
            Secure = true,
            SameSite = SameSiteMode.Strict // Igual que en Login
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
}
