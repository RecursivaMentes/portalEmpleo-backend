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
            Microsoft.AspNetCore.Http.BadHttpRequestException ex when ex.StatusCode == 400 => (StatusCodes.Status400BadRequest, "El JSON enviado no es valido.", "DATOS_INVALIDOS"),
            PostulacionException ex => (EstadoHttp(ex.CodigoError), ex.Message, ex.CodigoError),
            ServicioDependienteException ex =>
                (StatusCodes.Status503ServiceUnavailable,
                 "No pudimos completar esta operación en este momento. Volvé a intentarlo más tarde.",
                 ex.CodigoError),
            ArgumentException => (StatusCodes.Status400BadRequest, "Revisá los datos ingresados.", "DATOS_INVALIDOS"),
            Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException =>
                (StatusCodes.Status409Conflict, "La preparacion fue modificada. Volve a cargarla.", "PREPARACION_DESACTUALIZADA"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "No encontramos el recurso solicitado.", "RECURSO_NO_ENCONTRADO"),
            HttpRequestException ex when ex.StatusCode == HttpStatusCode.Unauthorized
                => (StatusCodes.Status503ServiceUnavailable, "No pudimos completar esta operación. Volvé a intentarlo más tarde.", "AUTENTICACION_INTERNA_RECHAZADA"),
            HttpRequestException ex when ex.StatusCode == HttpStatusCode.Forbidden
                => (StatusCodes.Status503ServiceUnavailable, "No pudimos completar esta operación. Volvé a intentarlo más tarde.", "AUTORIZACION_INTERNA_RECHAZADA"),
            HttpRequestException
                => (StatusCodes.Status503ServiceUnavailable, "No pudimos completar esta operación en este momento. Volvé a intentarlo más tarde.", "SERVICIO_DEPENDIENTE_NO_DISPONIBLE"),
            TaskCanceledException when (!cancellationToken.IsCancellationRequested)
                => (StatusCodes.Status503ServiceUnavailable, "La operación está tardando más de lo esperado. Volvé a intentarlo.", "SERVICIO_DEPENDIENTE_TIMEOUT"),
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
        "CAMBIO_NO_AUTORIZADO" or "CAMBIO_USUARIO_EN_CURSO" => StatusCodes.Status409Conflict,
        "ORDENAMIENTO_NO_DISPONIBLE" or "DATOS_EXTERNOS_INVALIDOS" => StatusCodes.Status503ServiceUnavailable,
        "OFERTA_NO_ENCONTRADA" or "CV_NO_ENCONTRADO" => StatusCodes.Status404NotFound,
        "TRABAJO_IA_ACTIVO" or "OFERTA_DESCARTADA" or "PENDIENTE_RETIRADO" or "OFERTA_NO_DISPONIBLE" or "OFERTA_FINALIZADA" or "PERFIL_INCOMPLETO" or "POSTULACION_DUPLICADA"
            or "PENDIENTE_DUPLICADO" or "PREPARACION_DUPLICADA" or "IA_OCUPADA" or "PREPARACION_EN_CURSO_OFERTA" or "ELIMINAR_POSTULACION_ACTIVA" or "PREPARACION_NO_CANCELABLE"
            or "PREPARACION_NO_EDITABLE" or "PREPARACION_DESACTUALIZADA" or "CAMBIOS_SIN_ACEPTAR" or "LIMITE_DIARIO_ALCANZADO" or "TRANSICION_ESTADO_INVALIDA"
            => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };
}