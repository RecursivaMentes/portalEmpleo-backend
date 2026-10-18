using Recomendaciones.Application.DTOs;
using Recomendaciones.Application.Services;
using Recomendaciones.Infrastructure.Services;

namespace Recomendaciones.Api.Endpoints;

public static class RecomendacionesEndpoints
{
    public static void MapRecomendacionesEndpoints(this WebApplication app)
    {
        app.MapPost("/api/recomendaciones/analizar",
            (AnalizarCompatibilidadRequest request, CompatibilidadService service) =>
            {
                if (request.UsuarioId == Guid.Empty ||
                    request.EmpleoId == Guid.Empty)
                {
                    return Results.BadRequest(
                        new { mensaje = "UsuarioId y EmpleoId son obligatorios." }
                    );
                }

                if (string.IsNullOrWhiteSpace(request.Requisitos))
                {
                    return Results.BadRequest(
                        new { mensaje = "Los requisitos del empleo son obligatorios." }
                    );
                }

                var resultado = service.Analizar(request);

                return Results.Ok(resultado);
            });
        
        app.MapGet("/api/recomendaciones/analizar/{usuarioId:guid}",
            async (
                Guid usuarioId,
                AnalisisOfertasService service,
                CancellationToken cancellationToken) =>
            {
                if (usuarioId == Guid.Empty)
                {
                    return Results.BadRequest(
                        new { mensaje = "El usuarioId es obligatorio." });
                }

                var resultados = await service.AnalizarAsync(
                    usuarioId, cancellationToken);

                if (resultados is null)
                {
                    return Results.NotFound(
                        new { mensaje = "No se encontró el perfil laboral del usuario." });
                }

                return Results.Ok(resultados);
            });
    }
}
