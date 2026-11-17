using System.Security.Claims;
using Application.Postulaciones;

namespace EmpleosYpostulaciones.Api.Endpoints;

// Estas acciones solo admiten JWT de usuario. No permiten actuar sobre otro usuario.
public static class ConfiguracionInternaEndpoints
{
    public static void MapConfiguracionInternaEndpoints(this WebApplication app)
    {
        var grupo = app.MapGroup("/api/postulaciones/control").RequireAuthorization();

        grupo.MapPost("/cambios/iniciar", async (ClaimsPrincipal usuario,
            IConfiguracionOperativaPostulaciones repo, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            var resultado = await repo.IniciarCambioAsync(usuarioId, ct);
            return resultado.Permitido ? Results.Ok(resultado) : Results.Conflict(resultado);
        });

        grupo.MapPost("/cambios/{operacionId:guid}/finalizar", async (
            Guid operacionId, ClaimsPrincipal usuario,
            IConfiguracionOperativaPostulaciones repo, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            await repo.FinalizarCambioAsync(usuarioId, operacionId, ct);
            return Results.NoContent();
        });

        grupo.MapGet("/configuracion", async (ClaimsPrincipal usuario,
            IConsultasProcesamientoInterno consultas, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            var resultado = await consultas.ConfiguracionAsync(usuarioId, ct);
            return resultado is null ? Results.NotFound() : Results.Ok(resultado);
        });

        grupo.MapPut("/configuracion", async (ConfiguracionProcesamiento solicitud,
            ClaimsPrincipal usuario, HttpRequest req,
            IConfiguracionOperativaPostulaciones repo, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            if (!Guid.TryParse(req.Headers["X-Cambio-Operacion-Id"].ToString(), out var operacionId))
                return Results.Conflict(new { mensaje = "El cambio no está autorizado. Volvé a intentarlo." });
            await repo.GuardarAsync(usuarioId, solicitud, operacionId, ct);
            return Results.NoContent();
        });
    }
}
