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

            try
            {
                var postulacion = await servicio.CrearAsync(
                    request, cancellationToken);

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
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(
                    new { mensaje = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(
                    new { mensaje = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(
                    new { mensaje = ex.Message });
            }
        });

        grupo.MapGet("/usuario/{usuarioId:guid}", async (
            Guid usuarioId,
            ClaimsPrincipal usuario,
            IPostulacionRepository repository,
            CancellationToken cancellationToken) =>
        {
            var usuarioIdToken = usuario.FindFirst("sub")?.Value;

            if (!Guid.TryParse(usuarioIdToken, out var usuarioAutenticado))
                return Results.Unauthorized();

            if (usuarioId != usuarioAutenticado)
                return Results.Forbid();

            var postulaciones = await repository.ObtenerPorUsuarioAsync(
                usuarioId, cancellationToken);

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
