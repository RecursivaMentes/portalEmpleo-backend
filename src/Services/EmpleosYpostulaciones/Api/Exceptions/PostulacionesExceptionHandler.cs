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
            ServicioDependienteException ex =>
                (StatusCodes.Status503ServiceUnavailable,
                 $"El servicio {ex.Servicio} no pudo completar la operacion. Volve a intentarlo mas tarde.",
                 ex.CodigoError),
            ArgumentException ex => (StatusCodes.Status400BadRequest, ex.Message, "DATOS_INVALIDOS"),
            Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException =>
                (StatusCodes.Status409Conflict, "La preparacion fue modificada. Volve a cargarla.", "PREPARACION_DESACTUALIZADA"),
            KeyNotFoundException ex => (StatusCodes.Status404NotFound, ex.Message, "RECURSO_NO_ENCONTRADO"),
            HttpRequestException ex when ex.StatusCode == HttpStatusCode.Unauthorized
                => (StatusCodes.Status503ServiceUnavailable, "Un servicio interno rechazó la autenticación entre servicios.", "AUTENTICACION_INTERNA_RECHAZADA"),
            HttpRequestException ex when ex.StatusCode == HttpStatusCode.Forbidden
                => (StatusCodes.Status503ServiceUnavailable, "Un servicio interno rechazó la autorización entre servicios.", "AUTORIZACION_INTERNA_RECHAZADA"),
            HttpRequestException
                => (StatusCodes.Status503ServiceUnavailable, "No fue posible completar la comunicación con un servicio dependiente.", "SERVICIO_DEPENDIENTE_NO_DISPONIBLE"),
            TaskCanceledException when (!cancellationToken.IsCancellationRequested)
                => (StatusCodes.Status503ServiceUnavailable, "Un servicio dependiente excedió el tiempo de espera.", "SERVICIO_DEPENDIENTE_TIMEOUT"),
            _ => (StatusCodes.Status500InternalServerError, "Ocurrió un error interno al procesar la solicitud.", "ERROR_INTERNO")
        };

        if (statusCode >= 500)
            logger.LogError(exception, "Error al procesar solicitud en EmpleosYpostulaciones");
        else
            logger.LogWarning(exception, "Solicitud de EmpleosYpostulaciones rechazada con HTTP {StatusCode}", statusCode);

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new { mensaje, codigo = statusCode,
            codigoError, seguimientoId = context.TraceIdentifier }, cancellationToken);
        return true;
    }

    private static int EstadoHttp(string codigoError) => codigoError switch
    {
        "DATOS_INVALIDOS" or "PRESENTACION_INVALIDA" or "SIN_CAMBIOS" => StatusCodes.Status400BadRequest,
        "SESION_NO_VALIDA" => StatusCodes.Status401Unauthorized,
        "ORDENAMIENTO_NO_DISPONIBLE" or "DATOS_EXTERNOS_INVALIDOS" => StatusCodes.Status503ServiceUnavailable,
        "OFERTA_NO_ENCONTRADA" or "CV_NO_ENCONTRADO" => StatusCodes.Status404NotFound,
        "OFERTA_NO_DISPONIBLE" or "OFERTA_FINALIZADA" or "PERFIL_INCOMPLETO" or "POSTULACION_DUPLICADA"
            or "PENDIENTE_DUPLICADO" or "PREPARACION_DUPLICADA" or "IA_OCUPADA" or "PREPARACION_NO_CANCELABLE"
            or "PREPARACION_NO_EDITABLE" or "PREPARACION_DESACTUALIZADA" or "CAMBIOS_SIN_ACEPTAR" or "LIMITE_DIARIO_ALCANZADO" or "TRANSICION_ESTADO_INVALIDA"
            => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };
}