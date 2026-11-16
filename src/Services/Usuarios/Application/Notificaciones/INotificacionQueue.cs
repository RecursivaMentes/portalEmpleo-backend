using Domain.Entities;

namespace Application.Notificaciones;

public interface INotificacionQueue
{
    ValueTask EnqueueAsync(
        Notificacion notificacion,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<Notificacion> ReadAllAsync(
        CancellationToken cancellationToken = default);
}