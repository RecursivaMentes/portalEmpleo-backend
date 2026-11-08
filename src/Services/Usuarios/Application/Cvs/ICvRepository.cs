namespace Application.Cvs;

public interface ICvRepository
{
    Task<bool> PerteneceAUsuarioAsync(Guid cvId, Guid usuarioId, CancellationToken cancellationToken = default);
}