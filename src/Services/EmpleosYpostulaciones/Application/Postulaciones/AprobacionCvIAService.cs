using Domain.Entities;
using Domain.Enums;

namespace Application.Postulaciones;

// La IA NUNCA invoca esta operacion. Solo es alcanzable desde una accion
// autenticada del usuario que vio la propuesta persistida.
public sealed class AprobacionCvIAService(
    IPendientesYPreparacionesRepository repositorio,
    IAplicacionCvAprobado aplicacion,
    IConsultasProcesamientoInterno consultas,
    IRegistroProcesamientoAutonomo registro,
    OrdenamientoPendientesService ordenamiento,
    PostulacionService postulaciones,
    IConfirmacionPreparacionRepository confirmacion)
{
    public async Task<PreparacionPostulacion?> AprobarAsync(Guid id, Guid usuarioId,
        int versionVista, CancellationToken ct = default)
    {
        var prep = await repositorio.ObtenerPreparacionAsync(id, usuarioId, ct);
        if (prep is null) return null;
        if (prep.VersionEdicion != versionVista ||
            prep.Estado is not (EstadoPreparacionPostulacion.PendienteAprobacionCv or EstadoPreparacionPostulacion.AplicandoCv) ||
            string.IsNullOrWhiteSpace(prep.CvTextoIAPropuesto) || prep.CambiosPendientes)
            throw PostulacionException.PreparacionDesactualizada();

        // Reclamar la ejecucion de forma optimista: evita aceptar la misma version dos veces.
        if (prep.Estado == EstadoPreparacionPostulacion.PendienteAprobacionCv)
        {
            prep.Estado = EstadoPreparacionPostulacion.AplicandoCv;
            prep.VersionEdicion++;
            prep.FechaActualizacion = DateTime.UtcNow;
            await repositorio.GuardarPreparacionAsync(prep, ct);
        }
        try
        {
            var cv = await aplicacion.AplicarAsync(prep.Id, usuarioId, prep.CvId,
                prep.CvNuevoIAPropuesto, prep.CvTituloIAPropuesto, prep.CvTextoIAPropuesto,
                prep.CvVersionIAOriginal, ct);
            prep.CvId = cv.Id;
            prep.CvTextoIAPropuesto = null;
            prep.CvTituloIAPropuesto = null;
            prep.CvVersionIAOriginal = null;
            prep.CvNuevoIAPropuesto = false;
            // El cupo asistido se contabiliza con bloqueo en PostgreSQL DESPUES de
            // resolver la aprobación, nunca antes de guardar el CV.
            prep.VersionEdicion++;
            prep.Estado = EstadoPreparacionPostulacion.PendienteAprobacion;
            prep.FechaActualizacion = DateTime.UtcNow;
            await repositorio.GuardarPreparacionAsync(prep, ct);
            // La aprobacion de CV NO autoriza envio en Asistida ni Desactivada.
            var config = await consultas.ConfiguracionAsync(usuarioId, ct);
            if (prep.Origen == OrigenPreparacionPostulacion.IniciativaIA &&
                prep.ModoEjecucion == "Asistida" && !prep.ContabilizadaEnCupo)
            {
                var (i, f) = ProcesamientoAutonomoService.DiaLocalUtc(DateTime.UtcNow,
                    config?.ZonaHoraria);
                if (config is null || !await registro.ReservarCupoPropuestaAsync(prep.Id,
                    usuarioId, config.LimiteDiario, i, f, ct))
                {
                    // El CV ya se aplicó correctamente. No declarar fallida ni
                    // invalidar la preparación por el cupo; dejarla para revisión.
                    prep.Estado = EstadoPreparacionPostulacion.PendienteAprobacion;
                    prep.UltimoError = "No se pudo contabilizar la propuesta asistida por el límite diario.";
                    prep.FechaActualizacion = DateTime.UtcNow;
                    await repositorio.GuardarPreparacionAsync(prep, ct);
                    return prep;
                }
            }
            if (config is null || !ProcesamientoAutonomoService.EnvioSinAprobacionPermitido(config.Modo)) return prep;
            // Una solicitud explicita no consume el limite diario, pero el procesamiento
            // iniciado por la IA debe revalidar cupo, compatibilidad y exclusiones.
            if (prep.Origen == OrigenPreparacionPostulacion.IniciativaIA)
            {
                if (config.LimiteDiario is < 1 or > 10 ||
                    config.CompatibilidadMinima is < 1 or > 100) return prep;
                var (inicio, fin) = ProcesamientoAutonomoService.DiaLocalUtc(DateTime.UtcNow, config.ZonaHoraria);
                if (await registro.CupoConsumidoAsync(usuarioId, inicio, fin, ct)
                    >= config.LimiteDiario) return prep;
                var pendientes = await ordenamiento.ListarInternoAsync(usuarioId, ct);
                var ofertaPendiente = pendientes.FirstOrDefault(x => x.EmpleoId == prep.EmpleoId);
                if (ofertaPendiente?.Compatibilidad is not { } score ||
                    score < config.CompatibilidadMinima) return prep;
                var oferta = await registro.ObtenerOfertaAsync(prep.EmpleoId, ct);
                if (oferta is null ||
                    ProcesamientoAutonomoService.Excluida(oferta, config.Preferencias?.Exclusiones))
                    return prep;
            }
            var postulacion = await postulaciones.ValidarYConstruirInternoAsync(
                new CrearPostulacionRequest(usuarioId, prep.EmpleoId,
                    cv.Id, prep.TextoPresentacion), ct);
            postulacion.Origen = prep.Origen == OrigenPreparacionPostulacion.IniciativaIA
                ? "Automatica" : "SolicitudUsuario";
            if (prep.Origen == OrigenPreparacionPostulacion.IniciativaIA)
            {
                var (i, f) = ProcesamientoAutonomoService.DiaLocalUtc(DateTime.UtcNow, config.ZonaHoraria);
                await confirmacion.ConfirmarEnvioConCupoAsync(usuarioId, prep.Id,
                    prep.VersionEdicion, postulacion, config.LimiteDiario, i, f, ct);
            }
            else
                await confirmacion.ConfirmarEnvioAsync(usuarioId, prep.Id,
                    prep.VersionEdicion, postulacion, ct);
            return await repositorio.ObtenerPreparacionAsync(id, usuarioId, ct) ?? prep;
        }
        catch (PostulacionException ex) when (ex.CodigoError is
            "DATOS_INVALIDOS" or "CV_NO_ENCONTRADO" or "PREPARACION_DESACTUALIZADA")
        {
            // Solo retroceder si Usuarios rechazó definitivamente la escritura.
            // Si el CV ya fue aplicado y el conflicto ocurrió durante el envio,
            // la preparación debe permanecer lista, nunca pedir otra aprobacion.
            if (prep.Estado == EstadoPreparacionPostulacion.AplicandoCv)
            {
                prep.Estado = EstadoPreparacionPostulacion.PendienteAprobacionCv;
                prep.UltimoError = ex.Message;
                prep.VersionEdicion++;
                prep.FechaActualizacion = DateTime.UtcNow;
                await repositorio.GuardarPreparacionAsync(prep, CancellationToken.None);
            }
            throw;
        }
        catch
        {
            // El resultado remoto puede haber quedado confirmado. NO retroceder ni
            // repetir con otra operacion. El recuperador usa el mismo Id idempotente.
            throw;
        }    }
}