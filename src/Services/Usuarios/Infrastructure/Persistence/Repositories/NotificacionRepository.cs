using Application.Notificaciones;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Usuarios.Infrastructure;

namespace Usuarios.Infrastructure.Persistence.Repositories;

public class NotificacionRepository(AppDbContext db) : INotificacionRepository
{
    public async Task AgregarAsync(
        Notificacion notificacion,
        CancellationToken cancellationToken = default)
    {
        await db.Notificaciones.AddAsync(notificacion, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Notificacion>> ObtenerPorUsuarioAsync(
        Guid usuarioId,
        bool soloNoLeidas = false,
        CancellationToken cancellationToken = default)
    {
        var consulta = db.Notificaciones
            .AsNoTracking()
            .Where(n => n.UsuarioId == usuarioId);

        if (soloNoLeidas)
        {
            consulta = consulta.Where(n => !n.Leido);
        }

        return await consulta
            .OrderByDescending(n => n.Fecha)
            .ToListAsync(cancellationToken);
    }

    public Task<int> ContarNoLeidasAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        return db.Notificaciones.CountAsync(
            n => n.UsuarioId == usuarioId && !n.Leido,
            cancellationToken);
    }

    public Task<Notificacion?> ObtenerPorIdAsync(
        Guid notificacionId,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        return db.Notificaciones.FirstOrDefaultAsync(
            n => n.Id == notificacionId && n.UsuarioId == usuarioId,
            cancellationToken);
    }

    public async Task MarcarComoLeidaAsync(
        Notificacion notificacion,
        CancellationToken cancellationToken = default)
    {
        notificacion.Leido = true;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarcarTodasComoLeidasAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var notificaciones = await db.Notificaciones
            .Where(n => n.UsuarioId == usuarioId && !n.Leido)
            .ToListAsync(cancellationToken);

        foreach (var notificacion in notificaciones)
        {
            notificacion.Leido = true;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}