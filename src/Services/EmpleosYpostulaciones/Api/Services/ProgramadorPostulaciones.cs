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
        // La reconciliacion de aprobaciones de CV SIEMPRE funciona. El envio
        // autonomo permanece sujeto a su propia configuracion.
        var minutos = Math.Clamp(configuracion.GetValue<int?>("PostulacionesIA:IntervaloMinutos") ?? 2, 1, 60);
        logger.LogInformation("Postulaciones IA: integración Chatbot={Integracion}, ciclo autónomo={Procesamiento}, intervalo={Minutos} min.",
            configuracion.GetValue<bool>("PostulacionesIA:IntegracionChatbotHabilitada"),
            configuracion.GetValue<bool>("PostulacionesIA:ProcesamientoHabilitado"), minutos);
        var segundos = Math.Clamp(configuracion.GetValue<int?>("PostulacionesIA:IntervaloSegundos") ?? minutos * 60, 10, 3600);
        logger.LogInformation("Intervalo efectivo del agente: {Segundos} segundos.", segundos);
        using var reloj = new PeriodicTimer(TimeSpan.FromSeconds(segundos));
        do
        {
            try
            {
                using var scope = fabrica.CreateScope();
                var motor = scope.ServiceProvider.GetRequiredService<ProcesamientoAutonomoService>();
                var registro = scope.ServiceProvider.GetRequiredService<IRegistroProcesamientoAutonomo>();
                await registro.RecuperarInterrumpidasAsync(DateTime.UtcNow.AddHours(-2), stoppingToken);
                var aprobaciones = scope.ServiceProvider.GetRequiredService<AprobacionCvIAService>();
                foreach (var pendiente in await registro.ObtenerCvEnAplicacionAsync(
                    DateTime.UtcNow.AddMinutes(-5), stoppingToken))
                {
                    try
                    {
                        await aprobaciones.AprobarAsync(pendiente.Id, pendiente.UsuarioId,
                            pendiente.VersionEdicion, stoppingToken);
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        logger.LogWarning(ex, "Ejecucion CV {PreparacionId} pendiente de reconciliacion.", pendiente.Id);
                    }
                }
                if (configuracion.GetValue<bool>("PostulacionesIA:ProcesamientoHabilitado"))
                {
                    if (!configuracion.GetValue<bool>("PostulacionesIA:IntegracionChatbotHabilitada"))
                    {
                        logger.LogWarning("Ciclo autónomo omitido: la integración con Chatbot está deshabilitada.");
                    }
                    else
                    {
                        logger.LogInformation("Iniciando ciclo de postulaciones autónomas.");
                        var completadas = await motor.EjecutarCicloAsync(stoppingToken);
                        logger.LogInformation("Ciclo de postulaciones autónomas finalizado. Preparaciones completadas: {Cantidad}.", completadas);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Error al ejecutar ciclo de postulaciones autonomas."); }
        }
        while (await reloj.WaitForNextTickAsync(stoppingToken));
    }
}
