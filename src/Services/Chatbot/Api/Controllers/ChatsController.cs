using Application.Chats;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/chats")]
public class ChatsController(ChatService chatService) : ControllerBase
{
    private const int MaxLongitudMensaje = 4000;

    [HttpPost]
    public async Task<IActionResult> Crear(
        [FromBody] CrearChatRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUsuarioId(out var usuarioId)) return TokenInvalido();

        var chat = await chatService.CrearChatAsync(
            usuarioId, request ?? new CrearChatRequest(), cancellationToken);

        return CreatedAtAction(nameof(ObtenerMensajes), new { chatId = chat.Id }, chat);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        if (!TryGetUsuarioId(out var usuarioId)) return TokenInvalido();

        return Ok(await chatService.ListarChatsAsync(usuarioId, cancellationToken));
    }

    [HttpGet("{chatId:guid}/mensajes")]
    public async Task<IActionResult> ObtenerMensajes(Guid chatId, CancellationToken cancellationToken)
    {
        if (!TryGetUsuarioId(out var usuarioId)) return TokenInvalido();

        var historial = await chatService.ObtenerHistorialAsync(usuarioId, chatId, cancellationToken);

        return historial is null ? ChatNoEncontrado() : Ok(historial);
    }

    [HttpPost("{chatId:guid}/mensajes")]
    public async Task<IActionResult> EnviarMensaje(
        Guid chatId,
        [FromBody] EnviarMensajeRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUsuarioId(out var usuarioId)) return TokenInvalido();

        if (string.IsNullOrWhiteSpace(request?.Contenido))
        {
            return BadRequest(new { message = "El mensaje no puede estar vacío." });
        }

        if (request.Contenido.Length > MaxLongitudMensaje)
        {
            return BadRequest(new { message = $"El mensaje no puede superar los {MaxLongitudMensaje} caracteres." });
        }

        try
        {
            var respuesta = await chatService.EnviarMensajeAsync(
                usuarioId, ObtenerAccessToken(), chatId, request, cancellationToken);

            return respuesta is null ? ChatNoEncontrado() : Ok(respuesta);
        }
        catch (AsistenteNoDisponibleException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "El asistente no está disponible en este momento. Intenta nuevamente más tarde."
            });
        }
    }

    /// <summary>Token de la petición actual, que se reenvía a Usuarios para consultar el perfil.</summary>
    private string ObtenerAccessToken()
    {
        var header = Request.Headers.Authorization.ToString();

        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim()
            : string.Empty;
    }

    private bool TryGetUsuarioId(out Guid usuarioId)
    {
        return Guid.TryParse(User.FindFirst("sub")?.Value, out usuarioId);
    }

    private IActionResult TokenInvalido() =>
        Unauthorized(new { message = "Token inválido o no proporcionado." });

    private IActionResult ChatNoEncontrado() =>
        NotFound(new { message = "Chat no encontrado." });
}
