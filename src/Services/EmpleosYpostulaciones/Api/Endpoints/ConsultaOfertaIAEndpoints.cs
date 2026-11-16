using Application.Empleos;
using EmpleosYpostulaciones.Api.Security;

namespace EmpleosYpostulaciones.Api.Endpoints;

public static class ConsultaOfertaIAEndpoints
{
    public static void MapConsultaOfertaIAEndpoints(this WebApplication app)
    {
        app.MapGet("/internal/empleos/{empleoId:guid}/para-ia", async (
            Guid empleoId, HttpRequest request, IConfiguration configuracion,
            EmpleoService servicio, CancellationToken ct) =>
        {
            if (!AutenticacionInterna.Autorizar(request, configuracion, "Chatbot"))
                return Results.Unauthorized();
            if (empleoId == Guid.Empty) return Results.BadRequest();
            var oferta = await servicio.ObtenerParaIaAsync(empleoId, ct);
            return oferta is null ? Results.NotFound() : Results.Ok(oferta);
        });
    }
}