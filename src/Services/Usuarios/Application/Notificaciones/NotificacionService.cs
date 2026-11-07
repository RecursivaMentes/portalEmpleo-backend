using Application.Auth;
using Application.Notificaciones.Responses;
using Domain.Entities;
using Domain.Enums;

namespace Application.Notificaciones;

public class NotificacionService(
    INotificacionRepository notificacionRepository,
    IUsuarioRepository usuarioRepository,
    INotificacionQueue notificacionQueue)
{
    public async Task<Guid> CrearAsync(
        Guid usuarioId,
        TipoNotificacion tipo,
        string mensaje,
        Guid? conversacionId = null,
        CancellationToken cancellationToken = default)
    {
        await ValidarUsuarioAsync(usuarioId, cancellationToken);

        if (string.IsNullOrWhiteSpace(mensaje))
            throw new ArgumentException("El mensaje no puede estar vacío.");

        mensaje = mensaje.Trim();

        if (mensaje.Length > 1000)
            throw new ArgumentException("El mensaje no puede superar los 1000 caracteres.");

        var notificacion = new Notificacion
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            ConversacionId = conversacionId,
            Tipo = tipo,
            Mensaje = mensaje,
            Leido = false,
            Fecha = DateTime.UtcNow
        };

        await notificacionQueue.EnqueueAsync(
            notificacion,
            cancellationToken);

        return notificacion.Id;
    }

    public async Task<IReadOnlyList<NotificacionResponse>> ObtenerPorUsuarioAsync(
        Guid usuarioId,
        bool soloNoLeidas = false,
        CancellationToken cancellationToken = default)
    {
        await ValidarUsuarioAsync(usuarioId, cancellationToken);

        var notificaciones = await notificacionRepository.ObtenerPorUsuarioAsync(
            usuarioId,
            soloNoLeidas,
            cancellationToken);

        return notificaciones.Select(n => new NotificacionResponse(
            n.Id,
            n.ConversacionId,
            n.Tipo.ToString(),
            n.Mensaje,
            n.Leido,
            n.Fecha
        )).ToList();
    }

    public async Task<int> ContarNoLeidasAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        await ValidarUsuarioAsync(usuarioId, cancellationToken);

        return await notificacionRepository.ContarNoLeidasAsync(
            usuarioId,
            cancellationToken);
    }

    public async Task<bool> MarcarComoLeidaAsync(
        Guid notificacionId,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        await ValidarUsuarioAsync(usuarioId, cancellationToken);

        var notificacion = await notificacionRepository.ObtenerPorIdAsync(
            notificacionId,
            usuarioId,
            cancellationToken);

        if (notificacion is null)
            return false;

        if (!notificacion.Leido)
        {
            await notificacionRepository.MarcarComoLeidaAsync(
                notificacion,
                cancellationToken);
        }

        return true;
    }

    public async Task MarcarTodasComoLeidasAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        await ValidarUsuarioAsync(usuarioId, cancellationToken);

        await notificacionRepository.MarcarTodasComoLeidasAsync(
            usuarioId,
            cancellationToken);
    }

    private async Task ValidarUsuarioAsync(
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        var usuario = await usuarioRepository.GetByIdAsync(
            usuarioId,
            cancellationToken);

        if (usuario is null || !usuario.Estado)
        {
            throw new InvalidOperationException(
                "El usuario no existe o se encuentra inactivo.");
        }
    }
}