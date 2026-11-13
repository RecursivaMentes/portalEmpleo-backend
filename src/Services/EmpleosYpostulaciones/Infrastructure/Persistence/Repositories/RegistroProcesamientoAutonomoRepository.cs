using Application.Postulaciones;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmpleosYpostulaciones.Infrastructure.Persistence.Repositories;

public sealed class RegistroProcesamientoAutonomoRepository(AppDbContext db) : IRegistroProcesamientoAutonomo
{
    public Task<List<Guid>> UsuariosConPendientesAsync(CancellationToken ct = default) =>
        db.EmpleosPendientes.AsNoTracking().Select(x => x.UsuarioId).Distinct().ToListAsync(ct);

    public Task<int> CupoConsumidoAsync(Guid usuarioId, DateTime inicioUtc, DateTime finUtc,
        CancellationToken ct = default) =>
        db.PreparacionesPostulacion.AsNoTracking().CountAsync(x =>
            x.UsuarioId == usuarioId && x.Origen == OrigenPreparacionPostulacion.IniciativaIA &&
            x.ContabilizadaEnCupo && x.FechaContabilizacionUtc >= inicioUtc &&
            x.FechaContabilizacionUtc < finUtc, ct);

    public async Task<bool> ReservarCupoPropuestaAsync(Guid preparacionId, Guid usuarioId,
        int limite, DateTime inicioUtc, DateTime finUtc, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await BloqueoCupoPostulacion.TomarAsync(db, usuarioId, ct);
        var prep = await db.PreparacionesPostulacion.FirstOrDefaultAsync(x =>
            x.Id == preparacionId && x.UsuarioId == usuarioId, ct);
        if (prep is null || prep.Estado != EstadoPreparacionPostulacion.PendienteAprobacion)
            return false;
        if (prep.ContabilizadaEnCupo) return true;
        var cantidad = await db.PreparacionesPostulacion.AsNoTracking().CountAsync(x =>
            x.UsuarioId == usuarioId && x.Origen == OrigenPreparacionPostulacion.IniciativaIA &&
            x.ContabilizadaEnCupo && x.FechaContabilizacionUtc >= inicioUtc &&
            x.FechaContabilizacionUtc < finUtc, ct);
        if (limite is < 1 or > 10 || cantidad >= limite) return false;
        prep.ContabilizadaEnCupo = true;
        prep.FechaContabilizacionUtc = DateTime.UtcNow;
        prep.VersionEdicion++;
        prep.FechaActualizacion = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public Task<int> IntentosEnPeriodoAsync(Guid usuarioId, Guid empleoId, DateTime desdeUtc,
        CancellationToken ct = default) =>
        db.IntentosPostulacionIA.AsNoTracking().CountAsync(x =>
            x.UsuarioId == usuarioId && x.EmpleoId == empleoId && x.FechaUtc >= desdeUtc &&
            x.Resultado != "Completada" && x.Resultado != "PropuestaLista", ct);

    public Task<Empleo?> ObtenerOfertaAsync(Guid empleoId, CancellationToken ct = default) =>
        db.Empleos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == empleoId, ct);

    public async Task<PreparacionPostulacion?> EstadoActualAsync(Guid preparacionId, Guid usuarioId,
        CancellationToken ct = default)
    {
        var seguimiento = db.ChangeTracker.Entries<PreparacionPostulacion>()
            .FirstOrDefault(x => x.Entity.Id == preparacionId);
        if (seguimiento is not null) seguimiento.State = EntityState.Detached;
        return await db.PreparacionesPostulacion.FirstOrDefaultAsync(x =>
            x.Id == preparacionId && x.UsuarioId == usuarioId, ct);
    }

    public Task GuardarPreparacionAsync(PreparacionPostulacion preparacion, CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);

    public async Task RegistrarIntentoAsync(IntentoProcesamientoAutonomo intento,
        CancellationToken ct = default)
    {
        db.IntentosPostulacionIA.Add(new IntentoPostulacionIA
        {
            Id = Guid.NewGuid(), UsuarioId = intento.UsuarioId, EmpleoId = intento.EmpleoId,
            PreparacionId = intento.PreparacionId, Resultado = intento.Resultado,
            Motivo = intento.Motivo is { Length: > 500 } ? intento.Motivo[..500] : intento.Motivo,
            FechaUtc = intento.FechaUtc
        });
        await db.SaveChangesAsync(ct);
    }

    public Task<List<PreparacionPostulacion>> ObtenerCvEnAplicacionAsync(DateTime limiteUtc, CancellationToken ct = default) =>
        db.PreparacionesPostulacion.AsNoTracking()
            .Where(x => x.Estado == EstadoPreparacionPostulacion.AplicandoCv && x.FechaActualizacion < limiteUtc)
            .ToListAsync(ct);

    public async Task<int> RecuperarInterrumpidasAsync(DateTime limiteUtc, CancellationToken ct = default)
    {
        // Solo recupera tareas que ya no recibieron actividad durante el plazo establecido.
        var canceladas = await db.PreparacionesPostulacion
            .Where(x => x.Estado == EstadoPreparacionPostulacion.CancelacionSolicitada &&
                x.FechaActualizacion < limiteUtc)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.Estado,
                EstadoPreparacionPostulacion.Cancelada).SetProperty(y => y.FechaActualizacion,
                DateTime.UtcNow), ct);
        var fallidas = await db.PreparacionesPostulacion
            .Where(x => (x.Estado == EstadoPreparacionPostulacion.Preparando ||
                         x.Estado == EstadoPreparacionPostulacion.EnModificacion) &&
                         x.FechaActualizacion < limiteUtc)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.Estado,
                EstadoPreparacionPostulacion.Fallida).SetProperty(y => y.UltimoError,
                "Ejecucion interrumpida sin confirmacion").SetProperty(y => y.FechaActualizacion,
                DateTime.UtcNow), ct);
        return canceladas + fallidas;
    }
}
