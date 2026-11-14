using Domain.Entities;
using Domain.Enums;

namespace Application.Postulaciones;

public sealed class SolicitudUsuarioIAService(
    PreparacionesService preparaciones,
    IPendientesYPreparacionesRepository repositorio,
    IConsultasProcesamientoInterno consultas,
    IPreparadorPostulacionesIA preparador,
    PostulacionService postulaciones,
    IConfirmacionPreparacionRepository confirmacion)
{
    public async Task<PreparacionPostulacion> SolicitarAsync(Guid usuarioId, Guid empleoId,
        CancellationToken ct = default)
    {
        if (!preparador.Disponible)
            throw new InvalidOperationException("La integracion del preparador IA no esta habilitada.");
        var configuracion = await consultas.ConfiguracionAsync(usuarioId, ct)
            ?? throw PostulacionException.DatosInvalidos();
        // Si ya existe trabajo para esta oferta, devolverlo sin llamar otra vez a IA.
        var existente = (await repositorio.ListarPreparacionesAsync(usuarioId, ct))
            .Where(x => x.EmpleoId == empleoId && x.Estado is not (
                EstadoPreparacionPostulacion.Cancelada or EstadoPreparacionPostulacion.Fallida or
                EstadoPreparacionPostulacion.Enviada))
            .OrderByDescending(x => x.FechaCreacion).FirstOrDefault();
        if (existente is not null) return existente;
        var validacion = await consultas.ValidarPerfilAsync(usuarioId, ct);
        if (validacion?.PerfilCompleto != true)
            throw PostulacionException.PerfilIncompleto(validacion?.Faltantes);
        PreparacionPostulacion preparacion;
        try
        {
            preparacion = await preparaciones.RegistrarInicioAsync(
                usuarioId, empleoId, OrigenPreparacionPostulacion.SolicitudUsuario, ct);
        }
        catch (PostulacionException ex) when (ex.CodigoError is "PREPARACION_DUPLICADA" or "IA_OCUPADA")
        {
            // Otra petición pudo reservar esa misma oferta entre la consulta y el alta.
            // Solo reutilizarla si es de esta oferta. Si la IA está en otra, rechazar.
            var concurrente = (await repositorio.ListarPreparacionesAsync(usuarioId, ct))
                .Where(x => x.EmpleoId == empleoId && x.Estado is not (
                    EstadoPreparacionPostulacion.Cancelada or EstadoPreparacionPostulacion.Fallida or
                    EstadoPreparacionPostulacion.Enviada))
                .OrderByDescending(x => x.FechaCreacion).FirstOrDefault();
            if (concurrente is null) throw;
            return concurrente;
        }
        preparacion.ModoEjecucion = configuracion.Modo ?? "Desactivada";
        try
        {
            var resultado = await preparador.PrepararAsync(usuarioId, empleoId, ct);
            // No alterar una preparacion si se solicito cancelacion durante la inferencia.
            var actual = await preparaciones.ObtenerAsync(preparacion.Id, usuarioId, ct)
                ?? throw new InvalidOperationException("Preparacion no encontrada.");
            if (actual.Estado == EstadoPreparacionPostulacion.CancelacionSolicitada)
            {
                actual.Estado = EstadoPreparacionPostulacion.Cancelada;
                actual.FechaActualizacion = DateTime.UtcNow;
                await repositorio.GuardarPreparacionAsync(actual, ct);
                return actual;
            }
            if (actual.Estado != EstadoPreparacionPostulacion.Preparando)
                throw PostulacionException.PreparacionDesactualizada();
            if (resultado.TextoPresentacion is { Length: > 4000 })
                throw PostulacionException.PresentacionInvalida();
            actual.CvTextoIAPropuesto = resultado.CvTextoPropuesto;
            actual.CvTituloIAPropuesto = resultado.CvTituloPropuesto;
            actual.CvVersionIAOriginal = resultado.CvVersionOriginal;
            actual.CvNuevoIAPropuesto = resultado.CrearCvNuevo;
            actual.CvId = resultado.CvId;
            actual.TextoPresentacion = resultado.TextoPresentacion;
            actual.Estado = resultado.RequiereAprobacionCv || !resultado.CvId.HasValue
                ? EstadoPreparacionPostulacion.PendienteAprobacionCv
                : EstadoPreparacionPostulacion.PendienteAprobacion;
            actual.FechaActualizacion = DateTime.UtcNow;
            await repositorio.GuardarPreparacionAsync(actual, ct);
            if (actual.Estado == EstadoPreparacionPostulacion.PendienteAprobacionCv)
                return actual;
            // Solicitud expresa no anula la configuracion: solo Automatica autoriza envio.
            var vigente = await consultas.ConfiguracionAsync(usuarioId, ct);
            if (!ProcesamientoAutonomoService.EnvioSinAprobacionPermitido(vigente?.Modo)) return actual;
            var postulacion = await postulaciones.ValidarYConstruirInternoAsync(
                new CrearPostulacionRequest(usuarioId, empleoId, actual.CvId!.Value,
                    actual.TextoPresentacion), ct);
            postulacion.Origen = "SolicitudUsuario";
            await confirmacion.ConfirmarEnvioAsync(usuarioId, actual.Id,
                actual.VersionEdicion, postulacion, ct);
            return await preparaciones.ObtenerAsync(actual.Id, usuarioId, ct) ?? actual;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch
        {
            var vigente = await preparaciones.ObtenerAsync(preparacion.Id, usuarioId,
                CancellationToken.None);
            if (vigente is not null && vigente.Estado != EstadoPreparacionPostulacion.Enviada)
            {
                vigente.Estado = EstadoPreparacionPostulacion.Fallida;
                vigente.FechaActualizacion = DateTime.UtcNow;
                await repositorio.GuardarPreparacionAsync(vigente, CancellationToken.None);
            }
            throw;
        }
    }
}