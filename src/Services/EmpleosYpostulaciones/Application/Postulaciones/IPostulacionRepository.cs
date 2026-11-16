using Domain.Entities;

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
}
