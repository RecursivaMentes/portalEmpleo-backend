using Microsoft.AspNetCore.Diagnostics;
using Recomendaciones.Infrastructure.Exceptions;

namespace Recomendaciones.Api.Exceptions;

public class RecomendacionesExceptionHandler : IExceptionHandler
{
    private readonly ILogger<RecomendacionesExceptionHandler> _logger;

    public RecomendacionesExceptionHandler(
        ILogger<RecomendacionesExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        int statusCode;
        string mensaje;

        switch (exception)
        {
            case ServicioExternoException ex:
                // Respuestas HTTP fallidas o problemas de formato
                // pueden distinguirse del servicio no disponible.
                statusCode = StatusCodes.Status503ServiceUnavailable;
                mensaje = $"El servicio de {ex.Servicio} no está disponible.";

                _logger.LogError(
                    exception,
                    "Error al consultar el microservicio {Servicio}",
                    ex.Servicio
                );
                break;

            case InvalidOperationException:
                statusCode = StatusCodes.Status422UnprocessableEntity;
                mensaje = "No se pudieron procesar los datos del perfil laboral.";

                _logger.LogWarning(
                    exception,
                    "Datos inválidos al analizar compatibilidad"
                );
                break;

            default:
                statusCode = StatusCodes.Status500InternalServerError;
                mensaje = "Ocurrió un error interno al procesar la solicitud.";

                _logger.LogError(
                    exception,
                    "Error inesperado en Recomendaciones"
                );
                break;
        }

        httpContext.Response.StatusCode = statusCode;

        await httpContext.Response.WriteAsJsonAsync(
            new
            {
                mensaje,
                codigo = statusCode
            },
            cancellationToken
        );

        return true;
    }
}