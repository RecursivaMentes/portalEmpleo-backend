using Application.Empleos;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmpleosYpostulaciones.Infrastructure.Persistence.Repositories;

public class EmpleoRepository(AppDbContext db) : IEmpleoRepository
{
    public async Task<List<EmpleoResultado>> BuscarPublicadosAsync(
        BuscarEmpleosFiltro filtros,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Empleo> query = db.Empleos
            .AsNoTracking()
            .Where(e => e.Estado == EstadoEmpleo.Publicado);

        if (!string.IsNullOrWhiteSpace(filtros.Busqueda))
        {
            string texto = $"%{filtros.Busqueda.Trim()}%";

            query = query.Where(e =>
                EF.Functions.ILike(e.Titulo, texto) ||
                EF.Functions.ILike(e.Descripcion, texto) ||
                (e.Requisitos != null &&
                 EF.Functions.ILike(e.Requisitos, texto)));
        }

        if (!string.IsNullOrWhiteSpace(filtros.Ubicacion))
        {
            string lugar = $"%{filtros.Ubicacion.Trim()}%";

            query = query.Where(e =>
                e.Ubicacion != null &&
                EF.Functions.ILike(e.Ubicacion, lugar));
        }

        if (filtros.Modalidad.HasValue)
        {
            query = query.Where(e => e.Modalidad == filtros.Modalidad.Value);
        }

        if (filtros.TipoContrato.HasValue)
        {
            query = query.Where(e => e.TipoContrato == filtros.TipoContrato.Value);
        }

        if (filtros.SalarioMin.HasValue)
        {
            query = query.Where(e =>
                e.SalarioMin.HasValue &&
                e.SalarioMin.Value >= filtros.SalarioMin.Value);
        }

        query = filtros.Orden?.ToLowerInvariant() switch
        {
            "titulo_asc" => query.OrderBy(e => e.Titulo),
            "titulo_desc" => query.OrderByDescending(e => e.Titulo),
            "fecha_asc" => query.OrderBy(e => e.FechaPublicacion),
            "salario_desc" => query.OrderBy(e => e.SalarioMax == null)
                                  .ThenByDescending(e => e.SalarioMax),
            _ => query.OrderByDescending(e => e.FechaPublicacion)
        };

        return await query
            .Select(e => new EmpleoResultado(
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
            ))
            .ToListAsync(cancellationToken);
    }
}
