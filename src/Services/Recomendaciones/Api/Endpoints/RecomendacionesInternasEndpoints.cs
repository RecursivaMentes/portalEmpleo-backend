using Recomendaciones.Api.Security;
using Recomendaciones.Application.Services;
using Recomendaciones.Infrastructure.Services;
using Recomendaciones.Infrastructure.Clients;

namespace Recomendaciones.Api.Endpoints;

public static class RecomendacionesInternasEndpoints
{
    public static void MapRecomendacionesInternasEndpoints(this WebApplication app)
    {
        app.MapGet("/internal/recomendaciones/usuarios/{usuarioId:guid}/analizar",
            async (Guid usuarioId, HttpRequest solicitud, IConfiguration configuracion,
                AnalisisOfertasService servicio, CancellationToken ct) =>
            {
                if (!AutenticacionInterna.Autorizar(solicitud, configuracion, "EmpleosYpostulaciones"))
                    return Results.Unauthorized();
                if (usuarioId == Guid.Empty) return Results.BadRequest();
                var clave = configuracion["InternalServices:Recomendaciones:Key"];
                if (string.IsNullOrWhiteSpace(clave))
                    return Results.Problem("Falta credencial interna de Recomendaciones.", statusCode: 503);
                var resultados = await servicio.AnalizarInternoAsync(usuarioId, clave, ct);
                return resultados is null ? Results.NotFound() : Results.Ok(resultados);
            });

        app.MapGet("/internal/recomendaciones/usuarios/{usuarioId:guid}/empleos/{empleoId:guid}/compatibilidad",
            async (Guid usuarioId, Guid empleoId, HttpRequest solicitud, IConfiguration configuracion,
                AnalisisOfertasService servicio, CancellationToken ct) =>
            {
                if (!AutenticacionInterna.Autorizar(solicitud, configuracion, "EmpleosYpostulaciones"))
                    return Results.Unauthorized();
                if (usuarioId == Guid.Empty || empleoId == Guid.Empty) return Results.BadRequest();
                var clave = configuracion["InternalServices:Recomendaciones:Key"];
                if (string.IsNullOrWhiteSpace(clave))
                    return Results.Problem("Falta credencial interna de Recomendaciones.", statusCode: 503);
                var (resultado, error) = await servicio.AnalizarEmpleoInternoAsync(usuarioId, empleoId, clave, ct);
                return error switch
                {
                    "PERFIL_NO_ENCONTRADO" or "EMPLEO_NO_ENCONTRADO" => Results.NotFound(),
                    "REQUISITOS_INSUFICIENTES" => Results.UnprocessableEntity(),
                    null when resultado is not null => Results.Ok(resultado),
                    _ => Results.Problem("No se pudo evaluar la oferta.")
                };
            });
    }
}
