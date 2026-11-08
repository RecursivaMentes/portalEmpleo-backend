namespace Application.Postulaciones;

public interface ICvValidationClient
{
    Task<bool> PerteneceAlUsuarioAsync(Guid cvId, string token, CancellationToken cancellationToken = default);
}