using Application.Postulaciones;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EmpleosYpostulaciones.Api.Services;

public sealed class ProgramadorPostulaciones(
    IServiceScopeFactory fabrica, IConfiguration configuracion,
    ILogger<ProgramadorPostulaciones> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuracion.GetValue<bool>("PostulacionesIA:ProcesamientoHabilitado"))
        {
            logger.LogInformation("Procesamiento IA desactivado hasta completar la integracion del agente.");
            return;
        }
        var minutos = Math.Clamp(configuracion.GetValue<int?>("PostulacionesIA:IntervaloMinutos") ?? 2, 1, 60);
        using var reloj = new PeriodicTimer(TimeSpan.FromMinutes(minutos));
        do
        {
            try
            {
                using var scope = fabrica.CreateScope();
                var motor = scope.ServiceProvider.GetRequiredService<ProcesamientoAutonomoService>();
                var registro = scope.ServiceProvider.GetRequiredService<IRegistroProcesamientoAutonomo>();
                await registro.RecuperarInterrumpidasAsync(DateTime.UtcNow.AddHours(-2), stoppingToken);
                await motor.EjecutarCicloAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Error al ejecutar ciclo de postulaciones autonomas."); }
        }
        while (await reloj.WaitForNextTickAsync(stoppingToken));
    }
}
