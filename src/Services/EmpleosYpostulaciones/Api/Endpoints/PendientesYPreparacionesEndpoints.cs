using System.Security.Claims;
using Application.Postulaciones;

namespace EmpleosYpostulaciones.Api.Endpoints;

public static class PendientesYPreparacionesEndpoints
{
    public sealed record AgregarPendienteRequest(Guid EmpleoId);
    public sealed record SolicitarCambiosIARequest(int VersionEdicion, string Instrucciones);

    private static string? Token(HttpRequest solicitud)
    {
        var autorizacion = solicitud.Headers.Authorization.ToString();
        if (!autorizacion.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        var token = autorizacion["Bearer ".Length..].Trim();
        return string.IsNullOrEmpty(token) ? null : token;
    }

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
            ClaimsPrincipal usuario, HttpRequest solicitud, OrdenamientoPendientesService servicio,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            var token = Token(solicitud);
            if (token is null) return Results.Unauthorized();
            return Results.Ok(await servicio.ListarAsync(usuarioId, token, ct));
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

        // Una solicitud expresa no cambia el modo: solo Automatica habilita envio directo.
        grupo.MapPost("/preparaciones/solicitar-con-ia", async (
            AgregarPendienteRequest request, ClaimsPrincipal usuario,
            IPreparadorPostulacionesIA preparador, SolicitudUsuarioIAService servicio,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            if (!preparador.Disponible) return Results.StatusCode(503);
            if (request.EmpleoId == Guid.Empty) return Results.BadRequest();
            var resultado = await servicio.SolicitarAsync(usuarioId, request.EmpleoId, ct);
            return Results.Ok(resultado);
        });

        grupo.MapGet("/preparaciones/{id:guid}/comparacion-cv-ia", async (
            Guid id, ClaimsPrincipal usuario, RevisionCvIAService servicio, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            var resultado = await servicio.CompararAsync(usuarioId, id, ct);
            return resultado is null ? Results.NotFound() : Results.Ok(resultado);
        });

        // Aprobar cambios de CV NO es postular. Solo el modo Automatico puede
        // continuar el envio sin una segunda aprobacion.
        grupo.MapPost("/preparaciones/{id:guid}/aprobar-cv-ia", async (
            Guid id, ConfirmarRevisionRequest request, ClaimsPrincipal usuario,
            AprobacionCvIAService servicio, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            var resultado = await servicio.AprobarAsync(id, usuarioId, request.VersionEdicion, ct);
            return resultado is null ? Results.NotFound() : Results.Ok(resultado);
        });

        grupo.MapPost("/preparaciones/{id:guid}/solicitar-cambios-ia", async (
            Guid id, SolicitarCambiosIARequest request, ClaimsPrincipal usuario,
            RevisionIAService servicio, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            var resultado = await servicio.SolicitarCambiosAsync(usuarioId, id,
                request.VersionEdicion, request.Instrucciones, ct);
            return resultado is null ? Results.NotFound() : Results.Ok(resultado);
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

        // Proponer solo prepara una previsualizacion; el usuario debe aceptarla expresamente.
        grupo.MapPost("/preparaciones/{id:guid}/proponer-cambios", async (
            Guid id, ProponerCambiosRequest request, ClaimsPrincipal usuario, HttpRequest solicitud,
            RevisionPreparacionesService servicio, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            var token = Token(solicitud);
            if (token is null) return Results.Unauthorized();
            var preparacion = await servicio.ProponerAsync(id, usuarioId, request, token, ct);
            return preparacion is null ? Results.NotFound() : Results.Ok(preparacion);
        });

        grupo.MapPost("/preparaciones/{id:guid}/aceptar-cambios", async (
            Guid id, ConfirmarRevisionRequest request, ClaimsPrincipal usuario, HttpRequest solicitud,
            RevisionPreparacionesService servicio, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            var token = Token(solicitud);
            if (token is null) return Results.Unauthorized();
            var preparacion = await servicio.AceptarCambiosAsync(id, usuarioId, request.VersionEdicion, token, ct);
            return preparacion is null ? Results.NotFound() : Results.Ok(preparacion);
        });

        grupo.MapPost("/preparaciones/{id:guid}/descartar-cambios", async (
            Guid id, ConfirmarRevisionRequest request, ClaimsPrincipal usuario,
            RevisionPreparacionesService servicio, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            var preparacion = await servicio.DescartarCambiosAsync(id, usuarioId, request.VersionEdicion, ct);
            return preparacion is null ? Results.NotFound() : Results.Ok(preparacion);
        });

        // Aprobar y enviar es la confirmacion del usuario: NO inicia otra tarea de IA.
        grupo.MapPost("/preparaciones/{id:guid}/enviar", async (
            Guid id, ConfirmarRevisionRequest request, ClaimsPrincipal usuario, HttpRequest solicitud,
            RevisionPreparacionesService servicio, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            var token = Token(solicitud);
            if (token is null) return Results.Unauthorized();
            var postulacion = await servicio.EnviarAsync(id, usuarioId, request.VersionEdicion, token, ct);
            return postulacion is null ? Results.NotFound() : Results.Created(
                $"/api/postulaciones/{postulacion.Id}", PostulacionResponse.DesdeEntidad(postulacion));
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
