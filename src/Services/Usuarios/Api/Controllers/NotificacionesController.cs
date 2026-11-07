using Application.Notificaciones;
using Application.Notificaciones.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Usuarios.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notificaciones")]
public class NotificacionesController(
    NotificacionService notificacionService) : ControllerBase
{
    private Guid? ObtenerUsuarioId()
    {
        var userIdToken = User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdToken, out var userId))
            return null;

        return userId;
    }

    [HttpGet]
    public async Task<IActionResult> Obtener(
        [FromQuery] bool soloNoLeidas = false,
        CancellationToken cancellationToken = default)
    {
        var usuarioId = ObtenerUsuarioId();

        if (usuarioId is null)
            return Unauthorized(new { message = "Token invalido." });

        var notificaciones = await notificacionService.ObtenerPorUsuarioAsync(
            usuarioId.Value,
            soloNoLeidas,
            cancellationToken);

        return Ok(notificaciones);
    }

    [HttpGet("no-leidas/count")]
    public async Task<IActionResult> ContarNoLeidas(
        CancellationToken cancellationToken)
    {
        var usuarioId = ObtenerUsuarioId();

        if (usuarioId is null)
            return Unauthorized(new { message = "Token invalido." });

        var cantidad = await notificacionService.ContarNoLeidasAsync(
            usuarioId.Value,
            cancellationToken);

        return Ok(new { cantidad });
    }

    [HttpPatch("{id:guid}/leer")]
    public async Task<IActionResult> MarcarComoLeida(
        Guid id,
        CancellationToken cancellationToken)
    {
        var usuarioId = ObtenerUsuarioId();

        if (usuarioId is null)
            return Unauthorized(new { message = "Token invalido." });

        var exito = await notificacionService.MarcarComoLeidaAsync(
            id,
            usuarioId.Value,
            cancellationToken);

        if (!exito)
            return NotFound(new { message = "Notificacion no encontrada." });

        return Ok(new { message = "Notificacion marcada como leida." });
    }

    [HttpPatch("leer-todas")]
    public async Task<IActionResult> MarcarTodasComoLeidas(
        CancellationToken cancellationToken)
    {
        var usuarioId = ObtenerUsuarioId();

        if (usuarioId is null)
            return Unauthorized(new { message = "Token invalido." });

        await notificacionService.MarcarTodasComoLeidasAsync(
            usuarioId.Value,
            cancellationToken);

        return Ok(new { message = "Todas las notificaciones fueron marcadas como leidas." });
    }

    [AllowAnonymous]
    [HttpPost("internas")]
    public async Task<IActionResult> CrearInterna(
    [FromBody] CrearNotificacionRequest request,
    [FromHeader(Name = "X-Internal-Api-Key")] string? apiKey,
    CancellationToken cancellationToken)
    {
        var claveConfigurada = HttpContext.RequestServices
            .GetRequiredService<IConfiguration>()["InternalApi:Key"];

        if (string.IsNullOrEmpty(claveConfigurada) ||
            string.IsNullOrEmpty(apiKey) ||
            !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(apiKey),
                System.Text.Encoding.UTF8.GetBytes(claveConfigurada)))
        {
            return Unauthorized(new { message = "Acceso no autorizado." });
        }

        var id = await notificacionService.CrearAsync(
            request.UsuarioId,
            request.Tipo,
            request.Mensaje,
            request.ConversacionId,
            cancellationToken);

        return Accepted(new { id, message = "Notificacion recibida." });
    }
}