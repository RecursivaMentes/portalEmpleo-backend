using Application.Notificaciones;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Usuarios.Infrastructure.Workers;

public class NotificacionWorker(
    INotificacionQueue notificacionQueue,
    IServiceScopeFactory scopeFactory,
    ILogger<NotificacionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker de notificaciones iniciado.");

        await foreach (
            var notificacion in notificacionQueue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();

                var repository = scope.ServiceProvider
                    .GetRequiredService<INotificacionRepository>();

                await repository.AgregarAsync(
                    notificacion,
                    stoppingToken);

                logger.LogInformation(
                    "Notificación {NotificacionId} procesada correctamente.",
                    notificacion.Id);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error al procesar la notificación {NotificacionId}.",
                    notificacion.Id);
            }
        }

        logger.LogInformation("Worker de notificaciones detenido.");
    }
}