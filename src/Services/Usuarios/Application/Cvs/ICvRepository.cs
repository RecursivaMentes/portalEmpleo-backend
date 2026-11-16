using Domain.Entities;

namespace Application.Cvs;

public sealed class CvConflictoException(string mensaje) : Exception(mensaje);

public interface ICvRepository
{
    Task<bool> PerteneceAUsuarioAsync(Guid cvId, Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// CVs de la persona, el actualizado más recientemente primero.
    /// Las entidades quedan rastreadas: los cambios se guardan con <see cref="GuardarCambiosAsync"/>.
    /// </summary>
    Task<List<Cv>> ListarPorUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Devuelve el CV solo si pertenece a la persona indicada.</summary>
    Task<Cv?> ObtenerAsync(Guid cvId, Guid usuarioId, CancellationToken cancellationToken = default);

    Task AgregarAsync(Cv cv, CancellationToken cancellationToken = default);

    /// <summary>Ejecuta una operacion de CV con bloqueo por usuario sin espera (PostgreSQL NOWAIT).</summary>
    Task<T> EjecutarExclusivoAsync<T>(Guid usuarioId, Func<Task<T>> accion, CancellationToken cancellationToken = default);

    void Eliminar(Cv cv);

    Task GuardarCambiosAsync(CancellationToken cancellationToken = default);
}
