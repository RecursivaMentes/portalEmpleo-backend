using System.Security.Claims;
using Application.Postulaciones;

namespace EmpleosYpostulaciones.Api.Endpoints;

public static class PostulacionesEndpoints
{
    public static void MapPostulacionesEndpoints(
        this WebApplication app)
    {
        var grupo = app.MapGroup("/api/postulaciones")
            .RequireAuthorization();

        grupo.MapPost("/", async (
            CrearPostulacionRequest request,
            ClaimsPrincipal usuario,
            HttpContext httpContext,
            PostulacionService servicio,
            CancellationToken cancellationToken) =>
        {
            var usuarioIdToken = usuario.FindFirst("sub")?.Value;

            if (!Guid.TryParse(usuarioIdToken, out var usuarioId))
                return Results.Unauthorized();

            if (request.UsuarioId != usuarioId)
            {
                return Results.Forbid();
            }

            // Se reutiliza el JWT validado por el middleware para consultar Usuarios.
            var authorization = httpContext.Request.Headers.Authorization.ToString();
            if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return Results.Unauthorized();
            var token = authorization.Substring("Bearer ".Length).Trim();
            if (string.IsNullOrWhiteSpace(token))
                return Results.Unauthorized();

            var postulacion = await servicio.CrearAsync(
                request, token, cancellationToken);

            return Results.Created(
                $"/api/postulaciones/{postulacion.Id}",
                PostulacionResponse.DesdeEntidad(postulacion));
        });

        grupo.MapGet("/me", async (
            ClaimsPrincipal usuario,
            PostulacionService servicio,
            CancellationToken cancellationToken) =>
        {
            var usuarioIdToken = usuario.FindFirst("sub")?.Value;

            if (!Guid.TryParse(usuarioIdToken, out var usuarioAutenticado))
                return Results.Unauthorized();

            var postulaciones = await servicio.ObtenerPorUsuarioAsync(
                usuarioAutenticado, cancellationToken);

            return Results.Ok(postulaciones);
        });

        // La ruta /{id:guid} evita conflictos con /me. Todo el grupo exige JWT.
        grupo.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal usuario,
            PostulacionService servicio,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();

            var postulacion = await servicio.ObtenerPorIdAsync(
                id, usuarioId, cancellationToken);

            // Mismo resultado cuando no existe o pertenece a otro usuario.
            return postulacion is null
                ? Results.NotFound()
                : Results.Ok(postulacion);
        });
    }
}
