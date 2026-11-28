using PortalEmpleo.Shared.Authentication;
using System.Security.Claims;
using Application.Postulaciones;

namespace EmpleosYpostulaciones.Api.Endpoints;

public static class PendientesYPreparacionesEndpoints
{
    public sealed record AgregarPendienteRequest(Guid EmpleoId);
    public sealed record SolicitarCambiosIARequest(int VersionEdicion, string Instrucciones);

    private static string? Token(HttpRequest solicitud) { return AuthenticatedToken.Read(solicitud); }

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

        grupo.MapGet("/descartadas/me", async (ClaimsPrincipal usuario,
            PendientesService servicio, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            return Results.Ok(await servicio.ListarDescartadasAsync(usuarioId, ct));
        });

        grupo.MapPut("/descartadas/{empleoId:guid}", async (Guid empleoId, ClaimsPrincipal usuario,
            PendientesService servicio, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            await servicio.DescartarOfertaAsync(usuarioId, empleoId, ct);
            return Results.NoContent();
        });

        grupo.MapDelete("/descartadas/{empleoId:guid}", async (Guid empleoId, ClaimsPrincipal usuario,
            PendientesService servicio, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            await servicio.RestaurarOfertaAsync(usuarioId, empleoId, ct);
            return Results.NoContent();
        });

        // Una solicitud expresa no cambia el modo: solo Automatica habilita envio directo.
        grupo.MapPost("/preparaciones/solicitar-con-ia", async (
            AgregarPendienteRequest request, ClaimsPrincipal usuario,
            IPreparadorPostulacionesIA preparador, SolicitudUsuarioIAService servicio,
            ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            if (!preparador.Disponible)
            {
                loggerFactory.CreateLogger("PostulacionesIA").LogWarning(
                    "Solicitud de preparación rechazada: PostulacionesIA:IntegracionChatbotHabilitada está desactivada.");
                return Results.Json(new
                {
                    mensaje = "La preparación con IA no está habilitada en este momento.",
                    codigo = 503,
                    codigoError = "IA_DESHABILITADA"
                }, statusCode: 503);
            }
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
            ClaimsPrincipal usuario, PreparacionesService servicio,
            IRegistroProcesamientoAutonomo registro, IConfiguration configuracion,
            DiagnosticoAgentePostulaciones diagnostico, CancellationToken ct) =>
        {
            if (!Guid.TryParse(usuario.FindFirst("sub")?.Value, out var usuarioId))
                return Results.Unauthorized();
            var (disponible, preparacion) = await servicio.DisponibilidadAsync(usuarioId, ct);
            var empleo = preparacion is null ? null : await registro.ObtenerOfertaAsync(preparacion.EmpleoId, ct);
            var ultima = await servicio.ObtenerUltimaAsync(usuarioId, ct);
            var ultimoResultado = await servicio.ObtenerUltimoResultadoAsync(usuarioId, ct);
            var ultimaOferta = ultima is null ? null : ultima.EmpleoId == empleo?.Id ? empleo :
                await registro.ObtenerOfertaAsync(ultima.EmpleoId, ct);
            return Results.Ok(new
            {
                disponible,
                preparacion,
                ultimaPreparacion = ultima is null ? null : new { ultima.Id, ultima.EmpleoId, ultima.Estado,
                    ultima.Origen, ultima.VersionEdicion, ultima.FechaActualizacion, ultima.PostulacionId,
                    titulo = ultimaOferta?.Titulo },
                ultimoResultado = ultimoResultado is null ? null : new { ultimoResultado.Id, ultimoResultado.Estado,
                    ultimoResultado.PostulacionId, ultimoResultado.EmpleoId, ultimoResultado.FechaActualizacion },
                evaluacion = diagnostico.Obtener(usuarioId),
                modo = await registro.ModoOperativoAsync(usuarioId, ct),
                cupoDiario = await registro.ObtenerCupoDiarioAsync(usuarioId, ct),
                intervaloSegundos = Math.Clamp(configuracion.GetValue<int?>("PostulacionesIA:IntervaloSegundos") ??
                    Math.Clamp(configuracion.GetValue<int?>("PostulacionesIA:IntervaloMinutos") ?? 2, 1, 60) * 60, 10, 3600),
                intervaloMinutos = Math.Clamp(configuracion.GetValue<int?>("PostulacionesIA:IntervaloMinutos") ?? 2, 1, 60),
                oferta = empleo is null ? null : new { empleo.Id, empleo.Titulo, empleo.EmpresaId },
                integracionHabilitada = configuracion.GetValue<bool>("PostulacionesIA:IntegracionChatbotHabilitada"),
                procesamientoHabilitado = configuracion.GetValue<bool>("PostulacionesIA:ProcesamientoHabilitado")
            });
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
