using Application.Postulaciones;
using Domain.Entities;
using Domain.Enums;
using EmpleosYpostulaciones.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EmpleosYpostulaciones.Infrastructure.Persistence.Repositories;

public sealed class PendientesYPreparacionesRepository(AppDbContext db) : IPendientesYPreparacionesRepository, IOrdenamientoPendientesRepository
{
    public Task<List<OfertaPendienteDatos>> ObtenerPendientesAsync(Guid usuarioId, CancellationToken ct = default) =>
        db.EmpleosPendientes.AsNoTracking()
            .Where(p => p.UsuarioId == usuarioId)
            .Join(db.Empleos.AsNoTracking(), p => p.EmpleoId, e => e.Id,
                (p, e) => new OfertaPendienteDatos(p.UsuarioId, p.EmpleoId, p.FechaAgregado,
                    e.Titulo, e.FechaCierre, e.Ubicacion, e.Modalidad, e.TipoContrato, e.SalarioMin, e.Estado))
            .ToListAsync(ct);

    public Task<EmpleoPendiente?> ObtenerPendienteAsync(Guid usuarioId, Guid empleoId, CancellationToken ct = default) =>
        db.EmpleosPendientes.AsNoTracking().FirstOrDefaultAsync(
            x => x.UsuarioId == usuarioId && x.EmpleoId == empleoId, ct);

    public Task<List<EmpleoPendiente>> ListarPendientesAsync(Guid usuarioId, CancellationToken ct = default) =>
        db.EmpleosPendientes.AsNoTracking().Where(x => x.UsuarioId == usuarioId)
            .OrderByDescending(x => x.FechaAgregado).ToListAsync(ct);

    public async Task AgregarPendienteAsync(EmpleoPendiente pendiente, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await BloqueoOfertaUsuario.TomarAsync(db, pendiente.UsuarioId, pendiente.EmpleoId, ct);
            var oferta = await db.Empleos.FromSqlInterpolated(
                $"SELECT * FROM empleos WHERE \"Id\" = {pendiente.EmpleoId} FOR UPDATE")
                .SingleOrDefaultAsync(ct);
            if (oferta is null) throw PostulacionException.OfertaNoEncontrada();
            if (oferta.Estado != EstadoEmpleo.Publicado)
                throw PostulacionException.OfertaNoDisponible();
            if (oferta.FechaCierre.HasValue && oferta.FechaCierre.Value <= DateTime.UtcNow)
                throw PostulacionException.OfertaFinalizada();
            // Segundo control DENTRO del bloqueo: evita guardar un pendiente si
            // otro hilo acaba de confirmar la postulacion desde cualquier pantalla.
            if (await db.Postulaciones.AnyAsync(x =>
                x.UsuarioId == pendiente.UsuarioId && x.EmpleoId == pendiente.EmpleoId && x.Estado != EstadoPostulacion.Retirada, ct))
                throw PostulacionException.Duplicada();
            // Guardar expresamente una oferta vuelve a habilitarla para la IA.
            await db.OfertasDescartadas.Where(x => x.UsuarioId == pendiente.UsuarioId &&
                x.EmpleoId == pendiente.EmpleoId).ExecuteDeleteAsync(ct);
            db.EmpleosPendientes.Add(pendiente);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (EsUnico(ex))
        {
            await tx.RollbackAsync(ct);
            db.Entry(pendiente).State = EntityState.Detached;
            throw PostulacionException.PendienteDuplicado();
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
    public async Task EliminarPendienteAsync(EmpleoPendiente pendiente, CancellationToken ct = default)
    {
        // Misma secuencia de bloqueos que el envío y que el inicio de IA.
        // Comprobar en la transacción evita retirar una oferta mientras se confirma.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await BloqueoCupoPostulacion.TomarAsync(db, pendiente.UsuarioId, ct);
        await BloqueoOfertaUsuario.TomarAsync(db, pendiente.UsuarioId, pendiente.EmpleoId, ct);
        var trabajoPendiente = await db.PreparacionesPostulacion.AsNoTracking().AnyAsync(p =>
            p.UsuarioId == pendiente.UsuarioId && p.EmpleoId == pendiente.EmpleoId &&
            p.Estado != EstadoPreparacionPostulacion.Cancelada &&
            p.Estado != EstadoPreparacionPostulacion.Fallida &&
            p.Estado != EstadoPreparacionPostulacion.Enviada, ct);
        if (trabajoPendiente) throw PostulacionException.TrabajoIaActivo();
        var vigente = await db.EmpleosPendientes.FirstOrDefaultAsync(p =>
            p.UsuarioId == pendiente.UsuarioId && p.EmpleoId == pendiente.EmpleoId, ct);
        if (vigente is not null)
        {
            db.EmpleosPendientes.Remove(vigente);
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    public Task<List<Guid>> ListarDescartadasAsync(Guid usuarioId, CancellationToken ct = default) =>
        db.OfertasDescartadas.AsNoTracking().Where(x => x.UsuarioId == usuarioId)
            .Select(x => x.EmpleoId).ToListAsync(ct);

    public async Task DescartarOfertaAsync(Guid usuarioId, Guid empleoId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await BloqueoCupoPostulacion.TomarAsync(db, usuarioId, ct);
        await BloqueoOfertaUsuario.TomarAsync(db, usuarioId, empleoId, ct);
        if (!await db.Empleos.AsNoTracking().AnyAsync(x => x.Id == empleoId, ct))
            throw PostulacionException.OfertaNoEncontrada();
        if (await ExistePreparacionNoFinalizadaAsync(usuarioId, empleoId, ct))
            throw PostulacionException.TrabajoIaActivo();
        if (!await db.OfertasDescartadas.AnyAsync(x => x.UsuarioId == usuarioId && x.EmpleoId == empleoId, ct))
            db.OfertasDescartadas.Add(new OfertaDescartada { UsuarioId = usuarioId, EmpleoId = empleoId });
        await db.EmpleosPendientes.Where(x => x.UsuarioId == usuarioId && x.EmpleoId == empleoId)
            .ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task RestaurarOfertaAsync(Guid usuarioId, Guid empleoId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await BloqueoCupoPostulacion.TomarAsync(db, usuarioId, ct);
        await BloqueoOfertaUsuario.TomarAsync(db, usuarioId, empleoId, ct);
        await db.OfertasDescartadas.Where(x => x.UsuarioId == usuarioId && x.EmpleoId == empleoId)
            .ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);
    }

    public Task<List<PreparacionPostulacion>> ListarPreparacionesAsync(Guid usuarioId, CancellationToken ct = default) =>
        db.PreparacionesPostulacion.AsNoTracking().Where(x => x.UsuarioId == usuarioId)
            .OrderByDescending(x => x.FechaCreacion).ToListAsync(ct);

    public Task<PreparacionPostulacion?> ObtenerPreparacionAsync(Guid id, Guid usuarioId, CancellationToken ct = default) =>
        db.PreparacionesPostulacion.FirstOrDefaultAsync(x => x.Id == id && x.UsuarioId == usuarioId, ct);

    public Task<PreparacionPostulacion?> ObtenerEstadoVigenteAsync(Guid id, Guid usuarioId, CancellationToken ct = default)
    {
        // Evitar que EF devuelva la entidad ya cargada antes de una llamada externa.
        var seguimiento = db.ChangeTracker.Entries<PreparacionPostulacion>()
            .FirstOrDefault(e => e.Entity.Id == id);
        if (seguimiento is not null) seguimiento.State = EntityState.Detached;
        return db.PreparacionesPostulacion.FirstOrDefaultAsync(x => x.Id == id && x.UsuarioId == usuarioId, ct);
    }

    public Task<PreparacionPostulacion?> ObtenerActivaAsync(Guid usuarioId, CancellationToken ct = default) =>
        db.PreparacionesPostulacion.AsNoTracking().FirstOrDefaultAsync(
            x => x.UsuarioId == usuarioId &&
                 (x.Estado == EstadoPreparacionPostulacion.Preparando ||
                  x.Estado == EstadoPreparacionPostulacion.EnModificacion ||
                  x.Estado == EstadoPreparacionPostulacion.AplicandoCv ||
                  x.Estado == EstadoPreparacionPostulacion.Enviando ||
                  x.Estado == EstadoPreparacionPostulacion.CancelacionSolicitada), ct);

    public Task<bool> ExistePreparacionNoFinalizadaAsync(Guid usuarioId, Guid empleoId, CancellationToken ct = default) =>
        db.PreparacionesPostulacion.AsNoTracking().AnyAsync(x =>
            x.UsuarioId == usuarioId && x.EmpleoId == empleoId &&
            x.Estado != EstadoPreparacionPostulacion.Cancelada &&
            x.Estado != EstadoPreparacionPostulacion.Enviada &&
            x.Estado != EstadoPreparacionPostulacion.Fallida, ct);

    public async Task AgregarPreparacionAsync(PreparacionPostulacion preparacion, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // El inicio de IA y la confirmacion manual comparten el MISMO bloqueo.
            await BloqueoCupoPostulacion.TomarAsync(db, preparacion.UsuarioId, ct);
            if (await db.ReservasCambioUsuario.AsNoTracking().AnyAsync(x =>
                x.UsuarioId == preparacion.UsuarioId && x.ExpiraUtc > DateTime.UtcNow, ct))
                throw PostulacionException.CambioUsuarioEnCurso();
            await BloqueoOfertaUsuario.TomarAsync(db, preparacion.UsuarioId, preparacion.EmpleoId, ct);
            var iaActiva = await db.PreparacionesPostulacion.AsNoTracking().AnyAsync(p =>
                p.UsuarioId == preparacion.UsuarioId &&
                (p.Estado == EstadoPreparacionPostulacion.Preparando ||
                 p.Estado == EstadoPreparacionPostulacion.EnModificacion ||
                 p.Estado == EstadoPreparacionPostulacion.AplicandoCv ||
                 p.Estado == EstadoPreparacionPostulacion.Enviando ||
                 p.Estado == EstadoPreparacionPostulacion.CancelacionSolicitada), ct);
            if (iaActiva) throw PostulacionException.IaOcupada();
            var oferta = await db.Empleos.FromSqlInterpolated(
                $"SELECT * FROM empleos WHERE \"Id\" = {preparacion.EmpleoId} FOR UPDATE")
                .SingleOrDefaultAsync(ct);
            if (oferta is null) throw PostulacionException.OfertaNoEncontrada();
            if (oferta.Estado != EstadoEmpleo.Publicado) throw PostulacionException.OfertaNoDisponible();
            if (oferta.FechaCierre.HasValue && oferta.FechaCierre.Value <= DateTime.UtcNow)
                throw PostulacionException.OfertaFinalizada();
            if (await db.Postulaciones.AsNoTracking().AnyAsync(p =>
                p.UsuarioId == preparacion.UsuarioId && p.EmpleoId == preparacion.EmpleoId && p.Estado != EstadoPostulacion.Retirada, ct))
                throw PostulacionException.Duplicada();
            if (preparacion.Origen == OrigenPreparacionPostulacion.IniciativaIA)
            {
                var modoVigente = await db.ConfiguracionesOperativas.AsNoTracking()
                    .Where(x => x.UsuarioId == preparacion.UsuarioId).Select(x => x.Modo).FirstOrDefaultAsync(ct);
                if (!ProcesamientoAutonomoService.Permitido(modoVigente) ||
                    (preparacion.ModoEjecucion is not null && preparacion.ModoEjecucion != modoVigente))
                    throw PostulacionException.PreparacionDesactualizada();
                if (await db.OfertasDescartadas.AsNoTracking().AnyAsync(x =>
                    x.UsuarioId == preparacion.UsuarioId && x.EmpleoId == preparacion.EmpleoId, ct))
                    throw PostulacionException.OfertaDescartada();
                if (preparacion.FuenteOferta != PreparacionPostulacion.FuenteRecomendaciones &&
                    !await db.EmpleosPendientes.AsNoTracking().AnyAsync(p =>
                        p.UsuarioId == preparacion.UsuarioId && p.EmpleoId == preparacion.EmpleoId, ct))
                    throw PostulacionException.PendienteRetirado();
            }
            db.PreparacionesPostulacion.Add(preparacion);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (EsUnico(ex))
        {
            await tx.RollbackAsync(ct);
            db.Entry(preparacion).State = EntityState.Detached;
            var restriccion = (ex.InnerException as PostgresException)?.ConstraintName ?? string.Empty;
            throw restriccion.Contains("UsuarioId_EmpleoId", StringComparison.Ordinal)
                ? PostulacionException.PreparacionDuplicada() : PostulacionException.IaOcupada();
        }
        catch
        {
            await tx.RollbackAsync(ct);
            if (db.Entry(preparacion).State == EntityState.Added)
                db.Entry(preparacion).State = EntityState.Detached;
            throw;
        }
    }

    public async Task GuardarPreparacionAsync(PreparacionPostulacion preparacion, CancellationToken ct = default)
    {
        // Incrementar versión en TODA escritura efectiva, incluso si el servicio
        // olvidó incrementarla. La versión original sigue siendo el token de EF.
        foreach (var entrada in db.ChangeTracker.Entries<PreparacionPostulacion>()
            .Where(e => e.State == EntityState.Modified))
        {
            var version = entrada.Property(x => x.VersionEdicion);
            if (version.CurrentValue == version.OriginalValue) version.CurrentValue++;
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (EsUnico(ex))
        {
            throw PostulacionException.IaOcupada();
        }
    }

    public async Task GuardarCancelacionAsync(PreparacionPostulacion propuesta, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await BloqueoCupoPostulacion.TomarAsync(db, propuesta.UsuarioId, ct);
        await BloqueoOfertaUsuario.TomarAsync(db, propuesta.UsuarioId, propuesta.EmpleoId, ct);
        var seguimiento = db.ChangeTracker.Entries<PreparacionPostulacion>()
            .FirstOrDefault(e => e.Entity.Id == propuesta.Id);
        if (seguimiento is not null) seguimiento.State = EntityState.Detached;
        var actual = await db.PreparacionesPostulacion.FromSqlInterpolated(
            $"SELECT * FROM preparaciones_postulacion WHERE \"Id\" = {propuesta.Id} AND \"UsuarioId\" = {propuesta.UsuarioId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (actual is null || actual.VersionEdicion != propuesta.VersionEdicion - 1)
            throw PostulacionException.PreparacionDesactualizada();
        var destino = propuesta.Estado;
        var permitido = (destino == EstadoPreparacionPostulacion.CancelacionSolicitada &&
            (actual.Estado is EstadoPreparacionPostulacion.Preparando or EstadoPreparacionPostulacion.EnModificacion)) ||
            (destino == EstadoPreparacionPostulacion.Cancelada &&
            (actual.Estado is EstadoPreparacionPostulacion.PendienteAprobacion or EstadoPreparacionPostulacion.PendienteAprobacionCv));
        if (!permitido) throw PostulacionException.PreparacionNoCancelable();
        actual.Estado = destino;
        actual.VersionEdicion++;
        actual.FechaActualizacion = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static bool EsUnico(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation;
}
