using Application.Postulaciones;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EmpleosYpostulaciones.Infrastructure.Persistence.Repositories;

public sealed class ConfirmacionPreparacionRepository(AppDbContext db) : IConfirmacionPreparacionRepository
{
    public Task<Postulacion> ConfirmarEnvioAsync(Guid usuarioId, Guid preparacionId,
        int versionValidada, Postulacion postulacion, CancellationToken ct = default) =>
        ConfirmarCoreAsync(usuarioId, preparacionId, versionValidada, postulacion, null, ct);

    public Task<Postulacion> ConfirmarEnvioAutomaticoAsync(Guid usuarioId, Guid preparacionId,
        int versionValidada, Postulacion postulacion, CancellationToken ct = default) =>
        ConfirmarCoreAsync(usuarioId, preparacionId, versionValidada, postulacion, null, ct, true);

    public Task<Postulacion> ConfirmarEnvioConCupoAsync(Guid usuarioId, Guid preparacionId,
        int versionValidada, Postulacion postulacion, int limite, DateTime inicioUtc,
        DateTime finUtc, CancellationToken ct = default) =>
        ConfirmarCoreAsync(usuarioId, preparacionId, versionValidada, postulacion,
            new RestriccionCupo(limite, inicioUtc, finUtc), ct, true);

    private sealed record RestriccionCupo(int Limite, DateTime InicioUtc, DateTime FinUtc);

    private async Task<Postulacion> ConfirmarCoreAsync(Guid usuarioId, Guid preparacionId,
        int versionValidada, Postulacion postulacion, RestriccionCupo? cupo, CancellationToken ct,
        bool automatico = false)
    {
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Siempre serializar con envio manual, inicio IA y reserva del cupo.
            await BloqueoCupoPostulacion.TomarAsync(db, usuarioId, ct);
            if (await db.ReservasCambioUsuario.AsNoTracking().AnyAsync(x =>
                x.UsuarioId == usuarioId && x.ExpiraUtc > DateTime.UtcNow, ct))
                throw PostulacionException.CambioUsuarioEnCurso();
            if (automatico)
            {
                var configVigente = await db.ConfiguracionesOperativas.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.UsuarioId == usuarioId, ct);
                if (configVigente?.Modo != "Automatica")
                    throw PostulacionException.PreparacionDesactualizada();
                if (cupo is not null)
                {
                    var (inicio, fin) = ProcesamientoAutonomoService.DiaLocalUtc(DateTime.UtcNow, configVigente.ZonaHoraria);
                    if (cupo.Limite != configVigente.LimiteDiario || cupo.InicioUtc != inicio || cupo.FinUtc != fin)
                        throw PostulacionException.PreparacionDesactualizada();
                }
            }
            await BloqueoOfertaUsuario.TomarAsync(db, usuarioId, postulacion.EmpleoId, ct);
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

            // La fuente de la preparación determina si se requiere un pendiente vigente.
            if (prep.Origen == OrigenPreparacionPostulacion.IniciativaIA)
            {
                if (await db.OfertasDescartadas.AsNoTracking().AnyAsync(x =>
                    x.UsuarioId == usuarioId && x.EmpleoId == prep.EmpleoId, ct))
                    throw PostulacionException.OfertaDescartada();
                if (prep.FuenteOferta != PreparacionPostulacion.FuenteRecomendaciones &&
                    !await db.EmpleosPendientes.AsNoTracking().AnyAsync(p =>
                        p.UsuarioId == usuarioId && p.EmpleoId == prep.EmpleoId, ct))
                    throw PostulacionException.PendienteRetirado();
            }

            // Revalidacion LOCAL dentro de la transaccion; todas las validaciones remotas
            // de perfil y CV se completaron antes de abrirla.
            var empleo = await db.Empleos
                .FromSqlInterpolated($"SELECT * FROM empleos WHERE \"Id\" = {prep.EmpleoId} FOR UPDATE")
                .SingleOrDefaultAsync(ct);
            if (empleo is null) throw PostulacionException.OfertaNoEncontrada();
            if (empleo.Estado != EstadoEmpleo.Publicado) throw PostulacionException.OfertaNoDisponible();
            if (empleo.FechaCierre.HasValue && empleo.FechaCierre.Value <= DateTime.UtcNow)
                throw PostulacionException.OfertaFinalizada();
            if (await db.Postulaciones.AnyAsync(x => x.UsuarioId == usuarioId && x.EmpleoId == prep.EmpleoId &&
                x.Estado != EstadoPostulacion.Retirada, ct))
                throw PostulacionException.Duplicada();

            if (cupo is not null && !prep.ContabilizadaEnCupo)
            {
                var gastados = await db.PreparacionesPostulacion.AsNoTracking().CountAsync(x =>
                    x.UsuarioId == usuarioId && x.Origen == OrigenPreparacionPostulacion.IniciativaIA &&
                    x.ContabilizadaEnCupo && x.FechaContabilizacionUtc >= cupo.InicioUtc &&
                    x.FechaContabilizacionUtc < cupo.FinUtc, ct);
                if (cupo.Limite is < 1 or > 10 || gastados >= cupo.Limite)
                    throw PostulacionException.LimiteDiarioAlcanzado();
            }
            // Una transaccion: enviar, cerrar preparacion y quitar de pendientes.
            postulacion.FuenteOferta = prep.FuenteOferta;
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
