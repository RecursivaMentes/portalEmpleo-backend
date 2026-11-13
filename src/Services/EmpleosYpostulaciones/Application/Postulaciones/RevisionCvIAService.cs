using Domain.Enums;

namespace Application.Postulaciones;

public sealed record CvParaCompararIA(Guid Id, long Version, string? TextoPlano);
public interface IConsultaCvParaCompararIA
{
    Task<CvParaCompararIA?> ObtenerAsync(Guid usuarioId, Guid cvId, CancellationToken ct = default);
}
public sealed record ComparacionCvIA(Guid PreparacionId, Guid? CvId,
    string? TextoOriginal, string TextoPropuesto, long? VersionOriginal,
    bool OriginalDesactualizado, bool CrearNuevo, bool AprobacionPrioritaria);

public sealed class RevisionCvIAService(IPendientesYPreparacionesRepository repo,
    IConsultaCvParaCompararIA cvs)
{
    public async Task<ComparacionCvIA?> CompararAsync(Guid usuarioId, Guid preparacionId,
        CancellationToken ct = default)
    {
        var prep = await repo.ObtenerPreparacionAsync(preparacionId, usuarioId, ct);
        if (prep is null) return null;
        if (prep.Estado != EstadoPreparacionPostulacion.PendienteAprobacionCv ||
            string.IsNullOrWhiteSpace(prep.CvTextoIAPropuesto))
            throw PostulacionException.PreparacionNoEditable();
        if (prep.CvNuevoIAPropuesto)
            return new(prep.Id, null, null, prep.CvTextoIAPropuesto,
                null, false, true, true);
        if (prep.CvId is not { } id) throw PostulacionException.CvNoEncontrado();
        var cv = await cvs.ObtenerAsync(usuarioId, id, ct)
            ?? throw PostulacionException.CvNoEncontrado();
        return new(prep.Id, id, cv.TextoPlano, prep.CvTextoIAPropuesto,
            prep.CvVersionIAOriginal, cv.Version != prep.CvVersionIAOriginal, false, true);
    }
}