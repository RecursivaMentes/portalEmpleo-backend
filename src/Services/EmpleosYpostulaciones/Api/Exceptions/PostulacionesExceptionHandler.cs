using Application.Postulaciones;
using Microsoft.AspNetCore.Diagnostics;

namespace EmpleosYpostulaciones.Api.Exceptions;

public sealed class PostulacionesExceptionHandler(ILogger<PostulacionesExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, mensaje) = exception switch
        {
            ArgumentException ex => (StatusCodes.Status400BadRequest, ex.Message),
            KeyNotFoundException ex => (StatusCodes.Status404NotFound, ex.Message),
            InvalidOperationException ex => (StatusCodes.Status409Conflict, ex.Message),
            HttpRequestException => (StatusCodes.Status503ServiceUnavailable, "No fue posible completar la validaciÃ³n con Usuarios."),
            TaskCanceledException when (!cancellationToken.IsCancellationRequested)
                => (StatusCodes.Status503ServiceUnavailable, "La validaciÃ³n con Usuarios excediÃ³ el tiempo de espera."),
            _ => (StatusCodes.Status500InternalServerError, "Ocurrio un error interno al procesar la solicitud.")
        };

        if (statusCode >= 500)
            logger.LogError(exception, "Error al procesar solicitud en EmpleosYpostulaciones");
        else
            logger.LogWarning(exception, "Solicitud de EmpleosYpostulaciones rechazada con HTTP {StatusCode}", statusCode);

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new { mensaje, codigo = statusCode }, cancellationToken);
        return true;
    }
}