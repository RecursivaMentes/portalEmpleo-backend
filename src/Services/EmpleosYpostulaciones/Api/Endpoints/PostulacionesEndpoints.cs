using System.Security.Claims;
using Application.Postulaciones;
using Domain.Enums;

namespace EmpleosYpostulaciones.Api.Endpoints;

public static class PostulacionesEndpoints
{
    public sealed record CambioEstadoRequest(EstadoPostulacion Estado);
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

        grupo.MapGet("/empleos/{empleoId:guid}", async (Guid empleoId,
            ClaimsPrincipal usuario, SeguimientoPostulacionesService servicio, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            var resultado = await servicio.VerPorOfertaAsync(empleoId, usuarioId, ct);
            return resultado is null ? Results.NotFound() : Results.Ok(resultado);
        });

        grupo.MapPost("/{id:guid}/estado", async (Guid id,
            CambioEstadoRequest request, ClaimsPrincipal usuario,
            SeguimientoPostulacionesService servicio, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            var resultado = await servicio.CambiarAsync(id, usuarioId, request.Estado, ct);
            return resultado is null ? Results.NotFound() : Results.Ok(resultado);
        });

        // Copia historica: un cambio posterior del CV original no altera el archivo enviado.
        grupo.MapGet("/{id:guid}/cv-enviado", async (Guid id, ClaimsPrincipal usuario,
            EmpleosYpostulaciones.Infrastructure.AppDbContext db, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            var p = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .FirstOrDefaultAsync(db.Postulaciones.AsQueryable().Where(x =>
                    x.Id == id && x.UsuarioId == usuarioId), ct);
            if (p?.CvDocumentoEnviado is null) return Results.NotFound();
            return Results.File(p.CvDocumentoEnviado,
                p.CvDocumentoMime ?? "application/pdf", p.CvDocumentoNombre ?? "cv-enviado.pdf");
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
