using Domain.Entities;
using Domain.Enums;
using EmpleosYpostulaciones.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EmpleosYpostulaciones.Api.Endpoints;

public static class EmpleosEndpoints
{
    public static void MapEmpleosEndpoints(this WebApplication app)
    {
        app.MapGet("/api/empleos", async (
            AppDbContext db,
            string? busqueda,
            string? ubicacion,
            ModalidadTrabajo? modalidad,
            TipoContrato? tipoContrato,
            decimal? salarioMin,
            string? orden
        ) =>
        {
            IQueryable<Empleo> query = db.Empleos
                .AsNoTracking()
                .Where(e => e.Estado == EstadoEmpleo.Publicado);

            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                string texto = $"%{busqueda.Trim()}%";

                query = query.Where(e =>
                    EF.Functions.ILike(e.Titulo, texto) ||
                    EF.Functions.ILike(e.Descripcion, texto) ||
                    (e.Requisitos != null &&
                     EF.Functions.ILike(e.Requisitos, texto)));
            }

            if (!string.IsNullOrWhiteSpace(ubicacion))
            {
                string lugar = $"%{ubicacion.Trim()}%";

                query = query.Where(e =>
                    e.Ubicacion != null &&
                    EF.Functions.ILike(e.Ubicacion, lugar));
            }

            if (modalidad.HasValue)
            {
                query = query.Where(e =>
                    e.Modalidad == modalidad.Value);
            }

            if (tipoContrato.HasValue)
            {
                query = query.Where(e =>
                    e.TipoContrato == tipoContrato.Value);
            }

            if (salarioMin.HasValue)
            {
                query = query.Where(e =>
                    e.SalarioMin.HasValue &&
                    e.SalarioMin.Value >= salarioMin.Value);
            }

            query = orden?.ToLowerInvariant() switch
            {
                "titulo_asc" => query.OrderBy(e => e.Titulo),

                "titulo_desc" => query.OrderByDescending(e => e.Titulo),

                "fecha_asc" => query.OrderBy(e => e.FechaPublicacion),

                "salario_desc" => query.OrderByDescending(e => e.SalarioMax),

                _ => query.OrderByDescending(e => e.FechaPublicacion)
            };

            var empleos = await query
                .Select(e => new
                {
                    e.Id,
                    e.Titulo,
                    e.Descripcion,
                    e.Requisitos,
                    e.Ubicacion,
                    e.Modalidad,
                    e.TipoContrato,
                    e.SalarioMin,
                    e.SalarioMax,
                    e.FechaPublicacion,
                    e.EmpresaId
                })
                .ToListAsync();

            return Results.Ok(empleos);
        });
    }
}
