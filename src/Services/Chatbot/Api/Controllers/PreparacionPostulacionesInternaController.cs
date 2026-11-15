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
            var mensaje = ex.CodigoError switch
            {
                "DATOS_INVALIDOS" => "Revisá los datos de la solicitud.",
                "CV_NO_ENCONTRADO" => "No encontramos el CV seleccionado.",
                "OFERTA_NO_DISPONIBLE" => "La oferta ya no está disponible.",
                "LIMITE_CVS" => "Alcanzaste el límite de CV que podés guardar.",
                "PERFIL_NO_DISPONIBLE" => "Completá tu perfil laboral antes de continuar.",
                "CVS_NO_DISPONIBLES" => "Necesitás tener al menos un CV disponible.",
                "CONTENIDO_NO_FUNDAMENTADO" => "No pudimos preparar una propuesta con información verificada. Revisá tu perfil.",
                _ => "No pudimos preparar la postulación. Volvé a intentarlo."
            };
            return StatusCode(estado, new { codigoError = ex.CodigoError, message = mensaje,
                seguimientoId = HttpContext.TraceIdentifier });
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
                message = "No pudimos obtener los datos necesarios para preparar la postulación." });
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