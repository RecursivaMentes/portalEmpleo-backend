using System.Threading.Channels;
using Application.Notificaciones;
using Domain.Entities;

namespace Usuarios.Infrastructure.Workers;

public class NotificacionQueue : INotificacionQueue
{
    private readonly Channel<Notificacion> _channel =
        Channel.CreateBounded<Notificacion>(
            new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

    public ValueTask EnqueueAsync(
        Notificacion notificacion,
        CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(notificacion, cancellationToken);
    }

    public IAsyncEnumerable<Notificacion> ReadAllAsync(
        CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}