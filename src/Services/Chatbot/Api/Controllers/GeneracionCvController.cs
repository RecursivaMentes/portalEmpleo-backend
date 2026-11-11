using Application.Chats;
using Application.Cvs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers;

/// <summary>
/// Generación de CV con IA a partir del perfil laboral (RF-11). El CV se guarda en Usuarios, que es quien
/// administra los CV (verlos, editarlos y descargarlos se hace allí: api/cvs).
/// </summary>
[ApiController]
[Authorize]
[Route("api/generacion-cv")]
public class GeneracionCvController(GeneracionCvService generacion, ILogger<GeneracionCvController> logger)
    : ControllerBase
{
    /// <summary>Genera un CV nuevo con IA y lo guarda. Responde 201 con el CV creado. Puede tardar.</summary>
    [HttpPost]
    public async Task<IActionResult> Generar(
        [FromBody] GenerarCvRequest? request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var usuarioId))
            return Unauthorized(new { message = "Token inválido o no proporcionado." });

        request ??= new GenerarCvRequest();

        if (request.PuestoObjetivo is { Length: > GeneracionCvService.MaxLargoPuestoObjetivo })
            return BadRequest(new
            {
                message = $"El puesto objetivo no puede superar los {GeneracionCvService.MaxLargoPuestoObjetivo} caracteres."
            });

        ResultadoGeneracion resultado;
        try
        {
            resultado = await generacion.GenerarAsync(usuarioId, ObtenerAccessToken(), request, cancellationToken);
        }
        catch (AsistenteNoDisponibleException ex)
        {
            logger.LogWarning(ex, "El asistente de IA no estuvo disponible al generar un CV.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "El asistente no está disponible en este momento. Intenta nuevamente más tarde."
            });
        }

        if (resultado.Exito)
            return StatusCode(StatusCodes.Status201Created, resultado.Cv);

        return resultado.Motivo switch
        {
            MotivoErrorGeneracion.YaEnCurso => Conflict(new { message = resultado.Mensaje }),
            MotivoErrorGeneracion.PerfilNoDisponible => StatusCode(503, new { message = resultado.Mensaje }),
            MotivoErrorGeneracion.PerfilIncompleto => BadRequest(new { message = resultado.Mensaje }),
            MotivoErrorGeneracion.IaNoFiable => StatusCode(502, new { message = resultado.Mensaje }),

            // El CV se redactó bien pero Usuarios no lo aceptó: se devuelve el texto para no perder el trabajo.
            _ => UnprocessableEntity(new { message = resultado.Mensaje, textoGenerado = resultado.TextoGenerado }),
        };
    }

    private string ObtenerAccessToken()
    {
        var header = Request.Headers.Authorization.ToString();

        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..].Trim()
            : string.Empty;
    }
}
