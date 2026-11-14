using Application.Chats;
using Application.Postulaciones;
using Chatbot.Api.Security;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers;

[ApiController]
[Route("internal/chatbot/postulaciones")]
public sealed class PreparacionPostulacionesInternaController(
    PreparacionPostulacionesIAService servicio, IConfiguration configuracion) : ControllerBase
{
    [HttpPost("preparar")]
    public async Task<IActionResult> Preparar([FromBody] PedidoPreparacionInterna? pedido, CancellationToken ct)
    {
        if (!AutenticacionInterna.Autorizar(Request, configuracion, "EmpleosYpostulaciones"))
            return Unauthorized();
        if (pedido is null) return BadRequest(new { codigoError = "DATOS_INVALIDOS",
            message = "Falta la solicitud de preparación." });
        try { return Ok(await servicio.PrepararAsync(pedido, ct)); }
        catch (PreparacionIAException ex)
        {
            var estado = ex.CodigoError switch
            {
                "DATOS_INVALIDOS" => 400,
                "CV_NO_ENCONTRADO" or "OFERTA_NO_DISPONIBLE" => 404,
                "LIMITE_CVS" => 409,
                "PERFIL_NO_DISPONIBLE" or "CVS_NO_DISPONIBLES" or "CONTENIDO_NO_FUNDAMENTADO" => 422,
                "DATOS_EXTERNOS_INVALIDOS" or "RESPUESTA_IA_INVALIDA" => 502,
                _ => 500
            };
            return StatusCode(estado, new { codigoError = ex.CodigoError, message = ex.Message });
        }
        catch (AsistenteNoDisponibleException)
        {
            return StatusCode(503, new { codigoError = "IA_NO_DISPONIBLE",
                message = "El asistente de preparación no está disponible." });
        }
        catch (DatosPreparacionIANoDisponiblesException ex)
        {
            return StatusCode(503, new {
                codigoError = "SERVICIO_" + ex.Servicio.ToUpperInvariant() + "_NO_DISPONIBLE",
                message = "No fue posible consultar " + ex.Servicio + " para preparar la postulación." });
        }
        catch (HttpRequestException)
        {
            return StatusCode(503, new { codigoError = "SERVICIO_DEPENDIENTE_NO_DISPONIBLE",
                message = "No fue posible consultar los datos necesarios para preparar la postulación." });
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return StatusCode(503, new { codigoError = "SERVICIO_DEPENDIENTE_TIMEOUT",
                message = "Se agotó el tiempo de espera al preparar la postulación." });
        }
    }
}