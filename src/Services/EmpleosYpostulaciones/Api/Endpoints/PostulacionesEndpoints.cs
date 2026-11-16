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
                new
                {
                    postulacion.Id,
                    postulacion.UsuarioId,
                    postulacion.EmpleoId,
                    postulacion.CvId,
                    postulacion.Estado,
                    postulacion.FechaPostulacion
                });
        });

        grupo.MapGet("/me", async (
            ClaimsPrincipal usuario,
            IPostulacionRepository repository,
            CancellationToken cancellationToken) =>
        {
            var usuarioIdToken = usuario.FindFirst("sub")?.Value;

            if (!Guid.TryParse(usuarioIdToken, out var usuarioAutenticado))
                return Results.Unauthorized();

            var postulaciones = await repository.ObtenerPorUsuarioAsync(
                usuarioAutenticado, cancellationToken);

            return Results.Ok(postulaciones.Select(p => new
            {
                p.Id,
                p.UsuarioId,
                p.EmpleoId,
                p.CvId,
                p.Estado,
                p.FechaPostulacion
            }));
        });
    }
}
