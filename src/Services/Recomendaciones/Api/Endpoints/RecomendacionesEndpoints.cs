using Recomendaciones.Application.DTOs;
using Recomendaciones.Application.Services;
using Recomendaciones.Infrastructure.Services;
using System.Security.Claims;

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
                ClaimsPrincipal usuario,
                HttpRequest solicitud,
                AnalisisOfertasService service,
                CancellationToken cancellationToken) =>
            {
                if (usuarioId == Guid.Empty)
                {
                    return Results.BadRequest(
                        new { mensaje = "El usuarioId es obligatorio." });
                }

                if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioAutenticado))
                    return Results.Unauthorized();
                if (usuarioAutenticado != usuarioId)
                    return Results.Forbid();

                var autorizacion = solicitud.Headers.Authorization.ToString();
                if (!autorizacion.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    return Results.Unauthorized();

                var accessToken = autorizacion["Bearer ".Length..].Trim();
                var resultados = await service.AnalizarAsync(
                    usuarioId, accessToken, cancellationToken);

                if (resultados is null)
                {
                    return Results.NotFound(
                        new { mensaje = "No se encontró el perfil laboral del usuario." });
                }

                return Results.Ok(resultados);
            }).RequireAuthorization();

        app.MapGet("/api/recomendaciones/usuario/{usuarioId:guid}",
            async (
                Guid usuarioId,
                ClaimsPrincipal usuario,
                HttpRequest solicitud,
                RecomendacionesService service,
                CancellationToken cancellationToken) =>
            {
                if (usuarioId == Guid.Empty)
                {
                    return Results.BadRequest(
                        new { mensaje = "El usuarioId es obligatorio." });
                }

                if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioAutenticado))
                    return Results.Unauthorized();
                if (usuarioAutenticado != usuarioId)
                    return Results.Forbid();

                var autorizacion = solicitud.Headers.Authorization.ToString();
                if (!autorizacion.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    return Results.Unauthorized();

                var accessToken = autorizacion["Bearer ".Length..].Trim();
                var recomendaciones = await service.ObtenerRecomendacionesAsync(
                    usuarioId, accessToken, cancellationToken);

                if (recomendaciones is null)
                {
                    return Results.NotFound(
                        new { mensaje = "No se encontró el perfil laboral del usuario." });
                }

                return Results.Ok(recomendaciones);
            }).RequireAuthorization();
    }
}
