using Domain.Entities;
using Domain.Enums;

namespace Application.Postulaciones;

public interface IPostulacionRepository
{
    Task<Empleo?> ObtenerEmpleoAsync(
        Guid empleoId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistePostulacionAsync(
        Guid usuarioId,
        Guid empleoId,
        CancellationToken cancellationToken = default);

    Task AgregarAsync(
        Postulacion postulacion,
        CancellationToken cancellationToken = default);

    Task<List<Postulacion>> ObtenerPorUsuarioAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default);

    Task<Postulacion?> ObtenerPorIdAsync(
        Guid postulacionId,
        CancellationToken cancellationToken = default);

    Task<List<Postulacion>?> ObtenerPorOfertaParaEmpresaAsync(Guid empleoId,
        Guid propietarioOfertaId, CancellationToken ct = default) =>
        throw new NotSupportedException("El repositorio no implementa consulta de empresa.");
    Task<Postulacion?> CambiarEstadoAsync(Guid postulacionId, Guid actorId,
        EstadoPostulacion nuevo, CancellationToken ct = default) =>
        throw new NotSupportedException("El repositorio no implementa cambios de estado.");
}
