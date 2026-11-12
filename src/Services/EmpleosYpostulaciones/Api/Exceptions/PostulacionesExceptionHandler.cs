using System.Net;
using Application.Postulaciones;
using Microsoft.AspNetCore.Diagnostics;

namespace EmpleosYpostulaciones.Api.Exceptions;

public sealed class PostulacionesExceptionHandler(ILogger<PostulacionesExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, mensaje, codigoError) = exception switch
        {
            PostulacionException ex => (EstadoHttp(ex.CodigoError), ex.Message, ex.CodigoError),
            ArgumentException ex => (StatusCodes.Status400BadRequest, ex.Message, "DATOS_INVALIDOS"),
            KeyNotFoundException ex => (StatusCodes.Status404NotFound, ex.Message, "RECURSO_NO_ENCONTRADO"),
            HttpRequestException ex when ex.StatusCode == HttpStatusCode.Unauthorized
                => (StatusCodes.Status401Unauthorized, "Usuarios rechazó la autenticación de esta solicitud.", "VALIDACION_NO_AUTORIZADA"),
            HttpRequestException ex when ex.StatusCode == HttpStatusCode.Forbidden
                => (StatusCodes.Status403Forbidden, "Usuarios no autorizó la validación de esta solicitud.", "VALIDACION_PROHIBIDA"),
            HttpRequestException
                => (StatusCodes.Status503ServiceUnavailable, "No fue posible completar la validación con Usuarios.", "SERVICIO_USUARIOS_NO_DISPONIBLE"),
            TaskCanceledException when (!cancellationToken.IsCancellationRequested)
                => (StatusCodes.Status503ServiceUnavailable, "La validación con Usuarios excedió el tiempo de espera.", "SERVICIO_USUARIOS_TIMEOUT"),
            _ => (StatusCodes.Status500InternalServerError, "Ocurrió un error interno al procesar la solicitud.", "ERROR_INTERNO")
        };

        if (statusCode >= 500)
            logger.LogError(exception, "Error al procesar solicitud en EmpleosYpostulaciones");
        else
            logger.LogWarning(exception, "Solicitud de EmpleosYpostulaciones rechazada con HTTP {StatusCode}", statusCode);

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new { mensaje, codigo = statusCode, codigoError }, cancellationToken);
        return true;
    }

    private static int EstadoHttp(string codigoError) => codigoError switch
    {
        "DATOS_INVALIDOS" or "PRESENTACION_INVALIDA" => StatusCodes.Status400BadRequest,
        "OFERTA_NO_ENCONTRADA" or "CV_NO_ENCONTRADO" => StatusCodes.Status404NotFound,
        "OFERTA_NO_DISPONIBLE" or "OFERTA_FINALIZADA" or "PERFIL_INCOMPLETO" or "POSTULACION_DUPLICADA"
            or "PENDIENTE_DUPLICADO" or "PREPARACION_DUPLICADA" or "IA_OCUPADA" or "PREPARACION_NO_CANCELABLE"
            => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };
}