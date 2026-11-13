using Application.Postulaciones;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EmpleosYpostulaciones.Infrastructure.Persistence.Repositories;

public sealed class ConfirmacionPreparacionRepository(AppDbContext db) : IConfirmacionPreparacionRepository
{
    public async Task<Postulacion> ConfirmarEnvioAsync(Guid usuarioId, Guid preparacionId,
        int versionValidada, Postulacion postulacion, CancellationToken ct = default)
    {
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // El servicio ya pudo consultar la entidad con seguimiento; evitamos usar su
            // copia en memoria al bloquear y releer el estado vigente en PostgreSQL.
            var seguimiento = db.ChangeTracker.Entries<PreparacionPostulacion>()
                .FirstOrDefault(e => e.Entity.Id == preparacionId);
            if (seguimiento is not null) seguimiento.State = EntityState.Detached;
            // Bloqueo para impedir que un mismo usuario confirme la misma preparacion dos veces.
            var prep = await db.PreparacionesPostulacion
                .FromSqlInterpolated($"SELECT * FROM preparaciones_postulacion WHERE \"Id\" = {preparacionId} AND \"UsuarioId\" = {usuarioId} FOR UPDATE")
                .SingleOrDefaultAsync(ct);
            if (prep is null || prep.Estado != EstadoPreparacionPostulacion.PendienteAprobacion ||
                prep.VersionEdicion != versionValidada || prep.CambiosPendientes ||
                prep.CvId != postulacion.CvId || prep.TextoPresentacion != postulacion.TextoPresentacion ||
                prep.EmpleoId != postulacion.EmpleoId || postulacion.UsuarioId != usuarioId)
                throw PostulacionException.PreparacionDesactualizada();

            // Revalidacion LOCAL dentro de la transaccion; todas las validaciones remotas
            // de perfil y CV se completaron antes de abrirla.
            var empleo = await db.Empleos
                .FromSqlInterpolated($"SELECT * FROM empleos WHERE \"Id\" = {prep.EmpleoId} FOR UPDATE")
                .SingleOrDefaultAsync(ct);
            if (empleo is null) throw PostulacionException.OfertaNoEncontrada();
            if (empleo.Estado != EstadoEmpleo.Publicado) throw PostulacionException.OfertaNoDisponible();
            if (empleo.FechaCierre.HasValue && empleo.FechaCierre.Value <= DateTime.UtcNow)
                throw PostulacionException.OfertaFinalizada();
            if (await db.Postulaciones.AnyAsync(x => x.UsuarioId == usuarioId && x.EmpleoId == prep.EmpleoId, ct))
                throw PostulacionException.Duplicada();

            // Una transaccion: enviar, cerrar preparacion y quitar de pendientes.
            db.Postulaciones.Add(postulacion);
            prep.PostulacionId = postulacion.Id;
            prep.Estado = EstadoPreparacionPostulacion.Enviada;
            if (prep.Origen == OrigenPreparacionPostulacion.IniciativaIA &&
                prep.ModoEjecucion == "Automatica" && !prep.ContabilizadaEnCupo)
            {
                // Cupo y envio se confirman en la misma transaccion.
                prep.ContabilizadaEnCupo = true;
                prep.FechaContabilizacionUtc = DateTime.UtcNow;
            }
            prep.VersionEdicion++;
            prep.FechaActualizacion = DateTime.UtcNow;
            var pendiente = await db.EmpleosPendientes.FirstOrDefaultAsync(
                x => x.UsuarioId == usuarioId && x.EmpleoId == prep.EmpleoId, ct);
            if (pendiente is not null) db.EmpleosPendientes.Remove(pendiente);
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
            return postulacion;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
                                            pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaccion.RollbackAsync(ct);
            throw PostulacionException.Duplicada(ex);
        }
        catch
        {
            await transaccion.RollbackAsync(ct);
            throw;
        }
    }
}
