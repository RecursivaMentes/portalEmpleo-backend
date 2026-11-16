using Domain.Entities;
using Domain.Enums;

namespace Application.Postulaciones;

public sealed class RevisionIAService(IPendientesYPreparacionesRepository repo,
    IPreparadorPostulacionesIA preparador)
{
    public async Task<PreparacionPostulacion?> SolicitarCambiosAsync(
        Guid usuarioId, Guid id, int versionVista, string instrucciones, CancellationToken ct = default)
    {
        if (usuarioId == Guid.Empty || id == Guid.Empty || string.IsNullOrWhiteSpace(instrucciones)
            || instrucciones.Length > 1000) throw PostulacionException.DatosInvalidos();
        if (!preparador.Disponible) throw new InvalidOperationException("Chatbot no disponible.");
        var prep = await repo.ObtenerPreparacionAsync(id, usuarioId, ct);
        if (prep is null) return null;
        if (prep.VersionEdicion != versionVista || prep.CambiosPendientes ||
            prep.Estado is not (EstadoPreparacionPostulacion.PendienteAprobacion or
                EstadoPreparacionPostulacion.PendienteAprobacionCv))
            throw PostulacionException.PreparacionDesactualizada();
        if (await repo.ObtenerActivaAsync(usuarioId, ct) is not null)
            throw PostulacionException.IaOcupada();
        var anterior = prep.Estado;
        prep.Estado = EstadoPreparacionPostulacion.EnModificacion;
        prep.VersionEdicion++;
        prep.FechaActualizacion = DateTime.UtcNow;
        await repo.GuardarPreparacionAsync(prep, ct);
        var versionTrabajo = prep.VersionEdicion;
        try
        {
            var resultado = await preparador.ModificarAsync(usuarioId, prep.EmpleoId, instrucciones,
                prep.CvId, prep.TextoPresentacion, ct);
            // La llamada externa puede durar minutos; recargar evitando la caché EF.
            var vigente = await repo.ObtenerEstadoVigenteAsync(id, usuarioId, ct)
                ?? throw PostulacionException.PreparacionDesactualizada();
            if (vigente.Estado == EstadoPreparacionPostulacion.CancelacionSolicitada)
            {
                vigente.Estado = EstadoPreparacionPostulacion.Cancelada;
                vigente.VersionEdicion++;
                vigente.FechaActualizacion = DateTime.UtcNow;
                await repo.GuardarPreparacionAsync(vigente, ct);
                return vigente;
            }
            if (vigente.Estado != EstadoPreparacionPostulacion.EnModificacion ||
                vigente.VersionEdicion != versionTrabajo)
                throw PostulacionException.PreparacionDesactualizada();
            if (resultado.TextoPresentacion is { Length: > 4000 })
                throw PostulacionException.PresentacionInvalida();
            vigente.CvId = resultado.CvId;
            vigente.TextoPresentacion = resultado.TextoPresentacion;
            vigente.CvTextoIAPropuesto = resultado.CvTextoPropuesto;
            vigente.CvTituloIAPropuesto = resultado.CvTituloPropuesto;
            vigente.CvVersionIAOriginal = resultado.CvVersionOriginal;
            vigente.CvNuevoIAPropuesto = resultado.CrearCvNuevo;
            vigente.Estado = resultado.RequiereAprobacionCv || !resultado.CvId.HasValue
                ? EstadoPreparacionPostulacion.PendienteAprobacionCv
                : EstadoPreparacionPostulacion.PendienteAprobacion;
            vigente.VersionEdicion++;
            vigente.FechaActualizacion = DateTime.UtcNow;
            await repo.GuardarPreparacionAsync(vigente, ct);
            return vigente;
        }
        catch
        {
            var vigente = await repo.ObtenerEstadoVigenteAsync(id, usuarioId, CancellationToken.None);
            if (vigente?.Estado == EstadoPreparacionPostulacion.CancelacionSolicitada)
            {
                vigente.Estado = EstadoPreparacionPostulacion.Cancelada;
                vigente.VersionEdicion++;
                vigente.FechaActualizacion = DateTime.UtcNow;
                await repo.GuardarPreparacionAsync(vigente, CancellationToken.None);
            }
            else if (vigente?.Estado == EstadoPreparacionPostulacion.EnModificacion &&
                     vigente.VersionEdicion == versionTrabajo)
            {
                vigente.Estado = anterior;
                vigente.VersionEdicion++;
                vigente.FechaActualizacion = DateTime.UtcNow;
                await repo.GuardarPreparacionAsync(vigente, CancellationToken.None);
            }
            throw;
        }
    }
}