using Application.Postulaciones;
using Domain.Entities;
using Domain.Enums;
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

    public async Task AgregarAsync(Postulacion postulacion, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await BloqueoOfertaUsuario.TomarAsync(db, postulacion.UsuarioId, postulacion.EmpleoId, ct);
            // Las comprobaciones remotas ya se realizaron. Revalidar oferta local
            // en la misma transaccion, igual que la confirmacion asistida.
            var oferta = await db.Empleos.FromSqlInterpolated(
                $"SELECT * FROM empleos WHERE \"Id\" = {postulacion.EmpleoId} FOR UPDATE")
                .SingleOrDefaultAsync(ct);
            if (oferta is null) throw PostulacionException.OfertaNoEncontrada();
            if (oferta.Estado != Domain.Enums.EstadoEmpleo.Publicado)
                throw PostulacionException.OfertaNoDisponible();
            if (oferta.FechaCierre.HasValue && oferta.FechaCierre.Value <= DateTime.UtcNow)
                throw PostulacionException.OfertaFinalizada();
            await db.Postulaciones.AddAsync(postulacion, ct);
            var pendiente = await db.EmpleosPendientes.FirstOrDefaultAsync(x =>
                x.UsuarioId == postulacion.UsuarioId && x.EmpleoId == postulacion.EmpleoId, ct);
            if (pendiente is not null) db.EmpleosPendientes.Remove(pendiente);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            pg.SqlState == PostgresErrorCodes.UniqueViolation &&
            pg.ConstraintName == "IX_postulaciones_UsuarioId_EmpleoId")
        {
            await tx.RollbackAsync(ct);
            throw PostulacionException.Duplicada(ex);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
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

    private sealed record EntradaHistorial(string Estado, DateTime FechaUtc);

    public Task<List<Postulacion>?> ObtenerPorOfertaParaEmpresaAsync(Guid empleoId,
        Guid propietarioOfertaId, CancellationToken ct = default) =>
        ObtenerPorOfertaCoreAsync(empleoId, propietarioOfertaId, ct);

    private async Task<List<Postulacion>?> ObtenerPorOfertaCoreAsync(Guid empleoId,
        Guid propietarioOfertaId, CancellationToken ct)
    {
        var propia = await db.Empleos.AsNoTracking().AnyAsync(x =>
            x.Id == empleoId && x.UsuarioId == propietarioOfertaId, ct);
        return propia ? await db.Postulaciones.AsNoTracking().Where(x => x.EmpleoId == empleoId)
            .OrderByDescending(x => x.FechaPostulacion).ToListAsync(ct) : null;
    }

    public static bool TransicionValida(EstadoPostulacion actual, EstadoPostulacion nuevo,
        bool esPostulante)
    {
        if (actual == nuevo) return true;
        if (esPostulante)
            return nuevo == EstadoPostulacion.Retirada && actual is
                EstadoPostulacion.Pendiente or EstadoPostulacion.EnRevision or EstadoPostulacion.Entrevista;
        return (actual, nuevo) switch
        {
            (EstadoPostulacion.Pendiente, EstadoPostulacion.EnRevision) => true,
            (EstadoPostulacion.Pendiente, EstadoPostulacion.Rechazada) => true,
            (EstadoPostulacion.EnRevision, EstadoPostulacion.Entrevista) => true,
            (EstadoPostulacion.EnRevision, EstadoPostulacion.Aceptada) => true,
            (EstadoPostulacion.EnRevision, EstadoPostulacion.Rechazada) => true,
            (EstadoPostulacion.Entrevista, EstadoPostulacion.Aceptada) => true,
            (EstadoPostulacion.Entrevista, EstadoPostulacion.Rechazada) => true,
            _ => false
        };
    }

    public async Task<Postulacion?> CambiarEstadoAsync(Guid postulacionId, Guid actorId,
        EstadoPostulacion nuevo, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var p = await db.Postulaciones.FromSqlInterpolated(
                $"SELECT * FROM postulaciones WHERE \"Id\" = {postulacionId} FOR UPDATE")
                .SingleOrDefaultAsync(ct);
            if (p is null) return null;
            var esPostulante = p.UsuarioId == actorId;
            var esEmpresa = await db.Empleos.AsNoTracking().AnyAsync(x =>
                x.Id == p.EmpleoId && x.UsuarioId == actorId, ct);
            if (!esPostulante && !esEmpresa) return null;
            if (!TransicionValida(p.Estado, nuevo, esPostulante))
                throw PostulacionException.EstadoInvalido();
            if (p.Estado != nuevo)
            {
                var entradas = System.Text.Json.JsonSerializer.Deserialize<List<EntradaHistorial>>(
                    p.HistorialEstadosJson, new System.Text.Json.JsonSerializerOptions(
                        System.Text.Json.JsonSerializerDefaults.Web)) ?? [];
                entradas.Add(new EntradaHistorial(nuevo.ToString(), DateTime.UtcNow));
                p.HistorialEstadosJson = System.Text.Json.JsonSerializer.Serialize(entradas,
                    new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
                p.Estado = nuevo;
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
            return p;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
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
