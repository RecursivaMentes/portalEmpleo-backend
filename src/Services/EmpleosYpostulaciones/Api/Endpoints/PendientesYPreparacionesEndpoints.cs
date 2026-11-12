using System.Security.Claims;
using Application.Postulaciones;

namespace EmpleosYpostulaciones.Api.Endpoints;

public static class PendientesYPreparacionesEndpoints
{
    public sealed record AgregarPendienteRequest(Guid EmpleoId);

    public static void MapPendientesYPreparacionesEndpoints(this WebApplication app)
    {
        var grupo = app.MapGroup("/api/postulaciones").RequireAuthorization();

        grupo.MapPost("/pendientes", async (AgregarPendienteRequest request,
            ClaimsPrincipal usuario, PendientesService servicio, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            var pendiente = await servicio.AgregarAsync(usuarioId, request.EmpleoId, ct);
            return Results.Created($"/api/postulaciones/pendientes/{pendiente.EmpleoId}", pendiente);
        });

        grupo.MapGet("/pendientes/me", async (
            ClaimsPrincipal usuario, PendientesService servicio, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            return Results.Ok(await servicio.ListarAsync(usuarioId, ct));
        });

        grupo.MapGet("/pendientes/{empleoId:guid}", async (
            Guid empleoId, ClaimsPrincipal usuario, PendientesService servicio, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            var pendiente = await servicio.ObtenerAsync(usuarioId, empleoId, ct);
            return pendiente is null ? Results.NotFound() : Results.Ok(pendiente);
        });

        grupo.MapDelete("/pendientes/{empleoId:guid}", async (
            Guid empleoId, ClaimsPrincipal usuario, PendientesService servicio, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            return await servicio.EliminarAsync(usuarioId, empleoId, ct)
                ? Results.NoContent() : Results.NotFound();
        });

        grupo.MapGet("/preparaciones/me", async (
            ClaimsPrincipal usuario, PreparacionesService servicio, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            return Results.Ok(await servicio.ListarAsync(usuarioId, ct));
        });

        grupo.MapGet("/preparaciones/activa/me", async (
            ClaimsPrincipal usuario, PreparacionesService servicio, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            var (disponible, preparacion) = await servicio.DisponibilidadAsync(usuarioId, ct);
            return Results.Ok(new { disponible, preparacion });
        });

        grupo.MapGet("/preparaciones/{id:guid}", async (
            Guid id, ClaimsPrincipal usuario, PreparacionesService servicio, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            var preparacion = await servicio.ObtenerAsync(id, usuarioId, ct);
            return preparacion is null ? Results.NotFound() : Results.Ok(preparacion);
        });

        grupo.MapPost("/preparaciones/{id:guid}/cancelar", async (
            Guid id, ClaimsPrincipal usuario, PreparacionesService servicio, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            var preparacion = await servicio.CancelarAsync(id, usuarioId, ct);
            if (preparacion is null) return Results.NotFound();
            return preparacion.Estado == Domain.Enums.EstadoPreparacionPostulacion.CancelacionSolicitada
                ? Results.Accepted($"/api/postulaciones/preparaciones/{id}", preparacion)
                : Results.Ok(preparacion);
        });
    }
}
