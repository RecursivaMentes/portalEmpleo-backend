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
        {
            return BadRequest(new { message = "El correo electrónico ya está registrado." });
        }

        return Ok(new { message = "Usuario registrado con éxito." });
    }

    [Authorize]
    [HttpPut("profile")] // Le pasamos el id por parámetro en la URL
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        //extraigo id del token
        var userIdToken = User.FindFirst("sub")?.Value;
        
        if (string.IsNullOrEmpty(userIdToken) || !Guid.TryParse(userIdToken, out var userId))
        {
            return Unauthorized(new { message = "Token inválido o no proporcionado." });
        }

        if(string.IsNullOrWhiteSpace(request.Nombre) && string.IsNullOrWhiteSpace(request.Apellido) 
            && string.IsNullOrWhiteSpace(request.Telefono) && string.IsNullOrWhiteSpace(request.PreferenciasJson) 
            && string.IsNullOrWhiteSpace(request.ReglasPostulacion)){
            return BadRequest(new { message = "Debe proporcionar al menos un campo para actualizar (Nombre, Apellido, etc.)." });
        }

        var exito = await authService.UpdateAsync(userId, request, cancellationToken);

        if (!exito)
        {
            return NotFound(new { message = "Usuario no encontrado." });
        }

        return Ok(new { message = "Perfil actualizado con éxito." });
    }
}