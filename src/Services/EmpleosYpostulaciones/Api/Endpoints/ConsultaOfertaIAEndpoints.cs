using EmpleosYpostulaciones.Api.Security;
using EmpleosYpostulaciones.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Domain.Enums;

namespace EmpleosYpostulaciones.Api.Endpoints;

public static class ConsultaOfertaIAEndpoints
{
    public static void MapConsultaOfertaIAEndpoints(this WebApplication app)
    {
        app.MapGet("/internal/empleos/{empleoId:guid}/para-ia", async (
            Guid empleoId, HttpRequest request, IConfiguration configuracion,
            AppDbContext db, CancellationToken ct) =>
        {
            if (!AutenticacionInterna.Autorizar(request, configuracion, "Chatbot"))
                return Results.Unauthorized();
            if (empleoId == Guid.Empty) return Results.BadRequest();
            var oferta = await db.Empleos.AsNoTracking().Where(x =>
                x.Id == empleoId && x.Estado == EstadoEmpleo.Publicado &&
                (x.FechaCierre == null || x.FechaCierre > DateTime.UtcNow))
                .Select(x => new { x.Id, x.Titulo, x.Descripcion, x.Requisitos,
                    x.Ubicacion, x.Modalidad, x.TipoContrato, x.FechaCierre })
                .FirstOrDefaultAsync(ct);
            return oferta is null ? Results.NotFound() : Results.Ok(oferta);
        });
    }
}