using Application.Chats;
using Application.Cvs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers;

public record AnalizarCvRequest(Guid CvId);

/// <summary>
/// Importar datos de un CV al perfil laboral (RF-09) y detectar qué datos faltan (RF-10).
/// Se hace en dos pasos para que la persona controle lo que entra a su perfil:
///   1. POST api/analisis-cv          → la IA lee el CV y devuelve una PROPUESTA. No se guarda nada.
///   2. POST api/analisis-cv/aplicar  → la persona confirma (o ajusta) y recién ahí se guarda en el perfil.
/// </summary>
[ApiController]
[Authorize]
[Route("api/analisis-cv")]
public class AnalisisCvController(
    AnalisisCvService analisis,
    AplicacionPropuestaService aplicacion,
    ILogger<AnalisisCvController> logger) : ControllerBase
{
    /// <summary>Lee un CV (que debe tener texto) y propone los datos para el perfil. No modifica nada.</summary>
    [HttpPost]
    public async Task<IActionResult> Analizar(
        [FromBody] AnalizarCvRequest? request, CancellationToken cancellationToken)
    {
        if (!TryGetUsuarioId(out var usuarioId)) return TokenInvalido();

        if (request is null || request.CvId == Guid.Empty)
            return BadRequest(new { message = "Indicá el CV que querés analizar (cvId)." });

        ResultadoAnalisisCv resultado;
        try
        {
            resultado = await analisis.AnalizarAsync(usuarioId, ObtenerAccessToken(), request.CvId, cancellationToken);
        }
        catch (AsistenteNoDisponibleException ex)
        {
            logger.LogWarning(ex, "El asistente de IA no estuvo disponible al analizar un CV.");
            return AsistenteCaido();
        }

        if (resultado.Exito) return Ok(resultado.Analisis);

        return resultado.Motivo switch
        {
            MotivoErrorAnalisis.YaEnCurso => Conflict(new { message = resultado.Mensaje }),
            MotivoErrorAnalisis.CvNoEncontrado => NotFound(new { message = resultado.Mensaje }),
            MotivoErrorAnalisis.CvSinTexto => BadRequest(new { message = resultado.Mensaje }),
            MotivoErrorAnalisis.ServicioNoDisponible => StatusCode(503, new { message = resultado.Mensaje }),
            _ => StatusCode(502, new { message = resultado.Mensaje }),
        };
    }

    /// <summary>
    /// Guarda en el perfil lo que la persona confirmó. Nunca borra ni modifica datos existentes: solo agrega
    /// lo que todavía no está. Con <c>iniciarChat</c> deja un chat abierto con las preguntas por lo que falta.
    /// </summary>
    [HttpPost("aplicar")]
    public async Task<IActionResult> Aplicar(
        [FromBody] AplicarPropuestaRequest? request, CancellationToken cancellationToken)
    {
        if (!TryGetUsuarioId(out var usuarioId)) return TokenInvalido();

        if (request is null)
            return BadRequest(new { message = "No hay nada para importar." });

        var resultado = await aplicacion.AplicarAsync(usuarioId, ObtenerAccessToken(), request, cancellationToken);

        if (resultado.Exito) return Ok(resultado.Resumen);

        return resultado.Motivo switch
        {
            MotivoErrorAplicacion.YaEnCurso => Conflict(new { message = resultado.Mensaje }),
            MotivoErrorAplicacion.PeticionInvalida => BadRequest(new { message = resultado.Mensaje }),
            MotivoErrorAplicacion.PerfilNoDisponible => StatusCode(503, new { message = resultado.Mensaje }),
            _ => UnprocessableEntity(new { message = resultado.Mensaje }),
        };
    }

    private IActionResult AsistenteCaido() =>
        StatusCode(StatusCodes.Status503ServiceUnavailable, new
        {
            message = "El asistente no está disponible en este momento. Intenta nuevamente más tarde."
        });

    private IActionResult TokenInvalido() =>
        Unauthorized(new { message = "Token inválido o no proporcionado." });

    private bool TryGetUsuarioId(out Guid usuarioId) =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out usuarioId);

    private string ObtenerAccessToken()
    {
        var header = Request.Headers.Authorization.ToString();

        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim()
            : string.Empty;
    }
}
