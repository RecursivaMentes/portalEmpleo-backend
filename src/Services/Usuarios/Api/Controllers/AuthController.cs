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

        return Ok(response);
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

        if ((request.Nombre is not null && string.IsNullOrWhiteSpace(request.Nombre)) ||
            (request.Apellido is not null && string.IsNullOrWhiteSpace(request.Apellido)))
        {
            return BadRequest(new { message = "El nombre y el apellido no pueden estar vacios." });
        }

        if (request.PreferenciasJson is not null)
        {
            try
            {
                using var json = JsonDocument.Parse(request.PreferenciasJson);
                if (json.RootElement.ValueKind != JsonValueKind.Object)
                    return UnprocessableEntity(new { message = "PreferenciasJson debe ser un objeto JSON." });
            }
            catch (JsonException)
            {
                return BadRequest(new { message = "PreferenciasJson no contiene JSON valido." });
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
