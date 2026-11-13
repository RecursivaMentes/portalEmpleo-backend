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
        try
        {
            var resultado = await preparador.ModificarAsync(usuarioId, prep.EmpleoId, instrucciones,
                prep.CvId, prep.TextoPresentacion, ct);
            if (resultado.TextoPresentacion is { Length: > 4000 })
                throw PostulacionException.PresentacionInvalida();
            prep.CvId = resultado.CvId;
            prep.TextoPresentacion = resultado.TextoPresentacion;
            prep.CvTextoIAPropuesto = resultado.CvTextoPropuesto;
            prep.CvTituloIAPropuesto = resultado.CvTituloPropuesto;
            prep.CvVersionIAOriginal = resultado.CvVersionOriginal;
            prep.CvNuevoIAPropuesto = resultado.CrearCvNuevo;
            prep.Estado = resultado.RequiereAprobacionCv || !resultado.CvId.HasValue
                ? EstadoPreparacionPostulacion.PendienteAprobacionCv
                : EstadoPreparacionPostulacion.PendienteAprobacion;
            prep.VersionEdicion++;
            prep.FechaActualizacion = DateTime.UtcNow;
            await repo.GuardarPreparacionAsync(prep, ct);
            return prep;
        }
        catch
        {
            if (prep.Estado == EstadoPreparacionPostulacion.EnModificacion)
            {
                prep.Estado = anterior;
                prep.VersionEdicion++;
                prep.FechaActualizacion = DateTime.UtcNow;
                await repo.GuardarPreparacionAsync(prep, CancellationToken.None);
            }
            throw;
        }
    }
}