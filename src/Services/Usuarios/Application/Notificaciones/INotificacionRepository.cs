using Domain.Entities;

namespace Application.Notificaciones;

public interface INotificacionRepository
{
    Task AgregarAsync(
        Notificacion notificacion,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Notificacion>> ObtenerPorUsuarioAsync(
        Guid usuarioId,
        bool soloNoLeidas = false,
        CancellationToken cancellationToken = default);

    Task<int> ContarNoLeidasAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default);

    Task<Notificacion?> ObtenerPorIdAsync(
        Guid notificacionId,
        Guid usuarioId,
        CancellationToken cancellationToken = default);

    Task MarcarComoLeidaAsync(
        Notificacion notificacion,
        CancellationToken cancellationToken = default);

    Task MarcarTodasComoLeidasAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default);
}
