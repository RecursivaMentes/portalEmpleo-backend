using Domain.Entities;
using Domain.Enums;

namespace Application.Postulaciones;

public sealed record ProponerCambiosRequest(Guid? CvId, string? TextoPresentacion);
public sealed record ConfirmarRevisionRequest(int VersionEdicion);

public sealed class RevisionPreparacionesService(
    IPendientesYPreparacionesRepository repositorio,
    ICvValidationClient cvValidation,
    PostulacionService postulaciones,
    IConfirmacionPreparacionRepository confirmacion)
{
    private async Task<PreparacionPostulacion?> ObtenerPendienteAsync(Guid id, Guid usuarioId, CancellationToken ct)
    {
        var preparacion = await repositorio.ObtenerPreparacionAsync(id, usuarioId, ct);
        if (preparacion is not null && preparacion.Estado != EstadoPreparacionPostulacion.PendienteAprobacion)
            throw PostulacionException.PreparacionNoEditable();
        return preparacion;
    }

    // PREVISUALIZACION: persistimos propuesta aparte, sin alterar los campos finales.
    public async Task<PreparacionPostulacion?> ProponerAsync(Guid id, Guid usuarioId,
        ProponerCambiosRequest request, string token, CancellationToken ct = default)
    {
        if (id == Guid.Empty || usuarioId == Guid.Empty || request is null)
            throw PostulacionException.DatosInvalidos();
        if (request.CvId == Guid.Empty)
            throw PostulacionException.DatosInvalidos();
        if (request.TextoPresentacion is { Length: > 4000 })
            throw PostulacionException.PresentacionInvalida();
        if (request.CvId is null && request.TextoPresentacion is null)
            throw PostulacionException.DatosInvalidos();
        var preparacion = await ObtenerPendienteAsync(id, usuarioId, ct);
        if (preparacion is null) return null;
        if (preparacion.CambiosPendientes)
            throw PostulacionException.CambiosSinAceptar();
        var cvPropuesto = request.CvId ?? preparacion.CvId;
        var textoPropuesto = request.TextoPresentacion ?? preparacion.TextoPresentacion;
        if (cvPropuesto == preparacion.CvId && textoPropuesto == preparacion.TextoPresentacion)
            throw PostulacionException.SinCambios();
        if (cvPropuesto.HasValue && !await cvValidation.PerteneceAlUsuarioAsync(cvPropuesto.Value, token, ct))
            throw PostulacionException.CvNoEncontrado();
        preparacion.CvIdPropuesto = cvPropuesto;
        preparacion.TextoPresentacionPropuesto = textoPropuesto;
        preparacion.CambiosPendientes = true;
        preparacion.VersionEdicion++;
        preparacion.FechaActualizacion = DateTime.UtcNow;
        await repositorio.GuardarPreparacionAsync(preparacion, ct);
        return preparacion;
    }

    public async Task<PreparacionPostulacion?> AceptarCambiosAsync(Guid id, Guid usuarioId,
        int versionVista, string token, CancellationToken ct = default)
    {
        var preparacion = await ObtenerPendienteAsync(id, usuarioId, ct);
        if (preparacion is null) return null;
        if (preparacion.VersionEdicion != versionVista)
            throw PostulacionException.PreparacionDesactualizada();
        if (!preparacion.CambiosPendientes)
            throw PostulacionException.SinCambios();
        if (preparacion.CvIdPropuesto.HasValue &&
            !await cvValidation.PerteneceAlUsuarioAsync(preparacion.CvIdPropuesto.Value, token, ct))
            throw PostulacionException.CvNoEncontrado();
        preparacion.CvId = preparacion.CvIdPropuesto;
        preparacion.TextoPresentacion = preparacion.TextoPresentacionPropuesto;
        LimpiarPropuesta(preparacion);
        await repositorio.GuardarPreparacionAsync(preparacion, ct);
        return preparacion;
    }

    public async Task<PreparacionPostulacion?> DescartarCambiosAsync(Guid id, Guid usuarioId,
        int versionVista, CancellationToken ct = default)
    {
        var preparacion = await ObtenerPendienteAsync(id, usuarioId, ct);
        if (preparacion is null) return null;
        if (preparacion.VersionEdicion != versionVista)
            throw PostulacionException.PreparacionDesactualizada();
        if (!preparacion.CambiosPendientes)
            throw PostulacionException.SinCambios();
        LimpiarPropuesta(preparacion);
        await repositorio.GuardarPreparacionAsync(preparacion, ct);
        return preparacion;
    }

    private static void LimpiarPropuesta(PreparacionPostulacion preparacion)
    {
        preparacion.CvIdPropuesto = null;
        preparacion.TextoPresentacionPropuesto = null;
        preparacion.CambiosPendientes = false;
        preparacion.VersionEdicion++;
        preparacion.FechaActualizacion = DateTime.UtcNow;
    }

    // La IA no participa en la confirmacion del modo asistido: el usuario envia.
    public async Task<Postulacion?> EnviarAsync(Guid id, Guid usuarioId, int versionVista,
        string token, CancellationToken ct = default)
    {
        var preparacion = await ObtenerPendienteAsync(id, usuarioId, ct);
        if (preparacion is null) return null;
        if (preparacion.VersionEdicion != versionVista)
            throw PostulacionException.PreparacionDesactualizada();
        if (preparacion.CambiosPendientes)
            throw PostulacionException.CambiosSinAceptar();
        if (!preparacion.CvId.HasValue || preparacion.CvId.Value == Guid.Empty)
            throw PostulacionException.CvNoEncontrado();
        var versionValidada = preparacion.VersionEdicion;
        var postulacion = await postulaciones.ValidarYConstruirAsync(new CrearPostulacionRequest(
            usuarioId, preparacion.EmpleoId, preparacion.CvId.Value,
            preparacion.TextoPresentacion), token, ct);
        postulacion.Origen = preparacion.Origen == OrigenPreparacionPostulacion.IniciativaIA
            ? "Asistida" : "SolicitudUsuario";
        return await confirmacion.ConfirmarEnvioAsync(usuarioId, id, versionValidada, postulacion, ct);
    }
}
