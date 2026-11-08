using Application.Postulaciones;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EmpleosYpostulaciones.Infrastructure.Persistence.Repositories;

public class PostulacionRepository(AppDbContext db) : IPostulacionRepository
{
    public Task<Empleo?> ObtenerEmpleoAsync(
        Guid empleoId,
        CancellationToken cancellationToken = default)
    {
        return db.Empleos
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == empleoId, cancellationToken);
    }

    public Task<bool> ExistePostulacionAsync(
        Guid usuarioId,
        Guid empleoId,
        CancellationToken cancellationToken = default)
    {
        return db.Postulaciones
            .AnyAsync(p =>
                p.UsuarioId == usuarioId &&
                p.EmpleoId == empleoId,
                cancellationToken);
    }

    public async Task AgregarAsync(
        Postulacion postulacion,
        CancellationToken cancellationToken = default)
    {
        await db.Postulaciones.AddAsync(postulacion, cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException pg &&
            pg.SqlState == PostgresErrorCodes.UniqueViolation &&
            pg.ConstraintName == "IX_postulaciones_UsuarioId_EmpleoId")
        {
            throw new InvalidOperationException("El usuario ya se postulÃ³ a esta oferta.");
        }
    }

    public Task<List<Postulacion>> ObtenerPorUsuarioAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        return db.Postulaciones
            .AsNoTracking()
            .Where(p => p.UsuarioId == usuarioId)
            .OrderByDescending(p => p.FechaPostulacion)
            .ToListAsync(cancellationToken);
    }

    public Task<Postulacion?> ObtenerPorIdAsync(
        Guid postulacionId,
        CancellationToken cancellationToken = default)
    {
        return db.Postulaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == postulacionId, cancellationToken);
    }
}
