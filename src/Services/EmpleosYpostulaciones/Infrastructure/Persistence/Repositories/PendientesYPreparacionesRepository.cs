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
                    e.Titulo, e.FechaCierre, e.Ubicacion, e.Modalidad, e.TipoContrato, e.SalarioMin))
            .ToListAsync(ct);

    public Task<EmpleoPendiente?> ObtenerPendienteAsync(Guid usuarioId, Guid empleoId, CancellationToken ct = default) =>
        db.EmpleosPendientes.AsNoTracking().FirstOrDefaultAsync(
            x => x.UsuarioId == usuarioId && x.EmpleoId == empleoId, ct);

    public Task<List<EmpleoPendiente>> ListarPendientesAsync(Guid usuarioId, CancellationToken ct = default) =>
        db.EmpleosPendientes.AsNoTracking().Where(x => x.UsuarioId == usuarioId)
            .OrderByDescending(x => x.FechaAgregado).ToListAsync(ct);

    public async Task AgregarPendienteAsync(EmpleoPendiente pendiente, CancellationToken ct = default)
    {
        db.EmpleosPendientes.Add(pendiente);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (EsUnico(ex))
        {
            throw PostulacionException.PendienteDuplicado();
        }
    }

    public async Task EliminarPendienteAsync(EmpleoPendiente pendiente, CancellationToken ct = default)
    {
        db.EmpleosPendientes.Remove(pendiente);
        await db.SaveChangesAsync(ct);
    }

    public Task<List<PreparacionPostulacion>> ListarPreparacionesAsync(Guid usuarioId, CancellationToken ct = default) =>
        db.PreparacionesPostulacion.AsNoTracking().Where(x => x.UsuarioId == usuarioId)
            .OrderByDescending(x => x.FechaCreacion).ToListAsync(ct);

    public Task<PreparacionPostulacion?> ObtenerPreparacionAsync(Guid id, Guid usuarioId, CancellationToken ct = default) =>
        db.PreparacionesPostulacion.FirstOrDefaultAsync(x => x.Id == id && x.UsuarioId == usuarioId, ct);

    public Task<PreparacionPostulacion?> ObtenerActivaAsync(Guid usuarioId, CancellationToken ct = default) =>
        db.PreparacionesPostulacion.AsNoTracking().FirstOrDefaultAsync(
            x => x.UsuarioId == usuarioId &&
                 (x.Estado == EstadoPreparacionPostulacion.Preparando ||
                  x.Estado == EstadoPreparacionPostulacion.EnModificacion ||
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
        db.PreparacionesPostulacion.Add(preparacion);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (EsUnico(ex))
        {
            // No dejar una entidad Added rastreada despues del intento rechazado.
            db.Entry(preparacion).State = EntityState.Detached;
            throw PostulacionException.IaOcupada();
        }
    }

    public async Task GuardarPreparacionAsync(PreparacionPostulacion preparacion, CancellationToken ct = default)
    {
        // Preparacion consultada con seguimiento y filtrada por UsuarioId.
        await db.SaveChangesAsync(ct);
    }

    private static bool EsUnico(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation;
}
