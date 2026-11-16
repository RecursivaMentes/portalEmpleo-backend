using Domain.Enums;

namespace Application.Postulaciones;

public sealed class SeguimientoPostulacionesService(IPostulacionRepository repositorio)
{
    public Task<CopiaHistoricaCv?> ObtenerDocumentoEnviadoAsync(Guid postulacionId,
        Guid actorId, CancellationToken ct = default)
    {
        if (postulacionId == Guid.Empty || actorId == Guid.Empty)
            throw PostulacionException.DatosInvalidos();
        return repositorio.ObtenerDocumentoEnviadoAsync(postulacionId, actorId, ct);
    }

    public async Task<IReadOnlyList<PostulacionResponse>?> VerPorOfertaAsync(Guid ofertaId,
        Guid propietarioId, CancellationToken ct = default)
    {
        if (ofertaId == Guid.Empty || propietarioId == Guid.Empty) throw PostulacionException.DatosInvalidos();
        var posts = await repositorio.ObtenerPorOfertaParaEmpresaAsync(ofertaId, propietarioId, ct);
        return posts?.Select(PostulacionResponse.DesdeEntidad).ToList();
    }

    public async Task<PostulacionResponse?> CambiarAsync(Guid postulacionId, Guid actorId,
        EstadoPostulacion nuevoEstado, CancellationToken ct = default)
    {
        if (postulacionId == Guid.Empty || actorId == Guid.Empty || !Enum.IsDefined(nuevoEstado))
            throw PostulacionException.DatosInvalidos();
        var actualizado = await repositorio.CambiarEstadoAsync(postulacionId, actorId, nuevoEstado, ct);
        return actualizado is null ? null : PostulacionResponse.DesdeEntidad(actualizado);
    }
}