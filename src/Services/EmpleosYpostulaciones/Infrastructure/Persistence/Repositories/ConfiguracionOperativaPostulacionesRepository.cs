using Application.Postulaciones;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmpleosYpostulaciones.Infrastructure.Persistence.Repositories;

public sealed class ConfiguracionOperativaPostulacionesRepository(AppDbContext db)
    : IConfiguracionOperativaPostulaciones
{
    private static ConfiguracionProcesamiento Combinar(
        ConfiguracionOperativaPostulacion c, ConfiguracionProcesamiento legada) =>
        new(c.Modo, Math.Clamp(c.CompatibilidadMinima, 50, 100), c.LimiteDiario,
            legada.Preferencias, c.OrdenPendientes, c.ZonaHoraria);

    public async Task<ConfiguracionProcesamiento> InicializarOLeerAsync(Guid usuarioId,
        ConfiguracionProcesamiento legada, CancellationToken ct = default)
    {
        var actual = await db.ConfiguracionesOperativas.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UsuarioId == usuarioId, ct);
        if (actual is not null) return Combinar(actual, legada);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await BloqueoCupoPostulacion.TomarAsync(db, usuarioId, ct);
        actual = await db.ConfiguracionesOperativas.FirstOrDefaultAsync(x => x.UsuarioId == usuarioId, ct);
        if (actual is null)
        {
            // Migracion paulatina de parametros anteriores; sin afectar las preferencias de Usuarios.
            actual = new ConfiguracionOperativaPostulacion
            {
                UsuarioId = usuarioId,
                Modo = legada.Modo ?? "Desactivada",
                CompatibilidadMinima = Math.Clamp(legada.CompatibilidadMinima, 50, 100),
                LimiteDiario = legada.LimiteDiario,
                OrdenPendientes = legada.OrdenPendientes ?? "Prioridad",
                ZonaHoraria = legada.ZonaHoraria ?? "America/Argentina/Buenos_Aires",
                Version = 1
            };
            db.ConfiguracionesOperativas.Add(actual);
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return Combinar(actual, legada);
    }

    public async Task GuardarAsync(Guid usuarioId, ConfiguracionProcesamiento nueva,
        Guid operacionId, CancellationToken ct = default)
    {
        if (usuarioId == Guid.Empty || operacionId == Guid.Empty ||
            nueva.Modo is not ("Automatica" or "Asistida" or "Desactivada") ||
            nueva.CompatibilidadMinima is < 50 or > 100 || nueva.LimiteDiario is < 1 or > 10)
            throw PostulacionException.DatosInvalidos();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await BloqueoCupoPostulacion.TomarAsync(db, usuarioId, ct);
        if (!await db.ReservasCambioUsuario.AsNoTracking().AnyAsync(x =>
            x.UsuarioId == usuarioId && x.OperacionId == operacionId && x.ExpiraUtc > DateTime.UtcNow, ct))
            throw PostulacionException.CambioNoAutorizado();
        var actual = await db.ConfiguracionesOperativas
            .FirstOrDefaultAsync(x => x.UsuarioId == usuarioId, ct);
        if (actual is null)
        {
            actual = new ConfiguracionOperativaPostulacion { UsuarioId = usuarioId };
            db.ConfiguracionesOperativas.Add(actual);
        }
        actual.Modo = nueva.Modo!;
        actual.CompatibilidadMinima = nueva.CompatibilidadMinima;
        actual.LimiteDiario = nueva.LimiteDiario;
        actual.OrdenPendientes = nueva.OrdenPendientes ?? "Prioridad";
        actual.ZonaHoraria = nueva.ZonaHoraria ?? "America/Argentina/Buenos_Aires";
        actual.Version++;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<InicioCambioUsuario> IniciarCambioAsync(Guid usuarioId,
        CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await BloqueoCupoPostulacion.TomarAsync(db, usuarioId, ct);
        var activo = await db.PreparacionesPostulacion.AsNoTracking()
            .Where(p => p.UsuarioId == usuarioId &&
                (p.Estado == EstadoPreparacionPostulacion.Preparando ||
                 p.Estado == EstadoPreparacionPostulacion.EnModificacion ||
                 p.Estado == EstadoPreparacionPostulacion.AplicandoCv ||
                 p.Estado == EstadoPreparacionPostulacion.Enviando ||
                 p.Estado == EstadoPreparacionPostulacion.CancelacionSolicitada))
            .OrderByDescending(p => p.FechaCreacion).FirstOrDefaultAsync(ct);
        if (activo is not null)
            return new InicioCambioUsuario(false, null, activo.Id,
                activo.VersionEdicion, activo.Estado.ToString());
        var ahora = DateTime.UtcNow;
        var reserva = await db.ReservasCambioUsuario.FirstOrDefaultAsync(x => x.UsuarioId == usuarioId, ct);
        if (reserva is not null && reserva.ExpiraUtc > ahora)
            return new InicioCambioUsuario(false, null, null, null, "CambioEnCurso");
        var operacionId = Guid.NewGuid();
        if (reserva is null)
        {
            reserva = new ReservaCambioUsuario { UsuarioId = usuarioId };
            db.ReservasCambioUsuario.Add(reserva);
        }
        reserva.OperacionId = operacionId;
        reserva.ExpiraUtc = ahora.AddMinutes(5);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new InicioCambioUsuario(true, operacionId, null, null, null);
    }

    public async Task FinalizarCambioAsync(Guid usuarioId, Guid operacionId,
        CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await BloqueoCupoPostulacion.TomarAsync(db, usuarioId, ct);
        var reserva = await db.ReservasCambioUsuario.FirstOrDefaultAsync(x =>
            x.UsuarioId == usuarioId && x.OperacionId == operacionId, ct);
        if (reserva is not null)
        {
            db.ReservasCambioUsuario.Remove(reserva);
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
}
