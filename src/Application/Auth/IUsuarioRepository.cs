using Domain.Entities;

namespace Application.Auth;

public interface IUsuarioRepository
{
    Task<Usuario?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);
}
