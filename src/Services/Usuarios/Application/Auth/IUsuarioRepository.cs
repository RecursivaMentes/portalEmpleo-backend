using Domain.Entities;

namespace Application.Auth;

public interface IUsuarioRepository
{
    Task<Usuario?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);
    
    Task<Usuario?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Usuario usuario, CancellationToken cancellationToken = default);

    Task UpdateAsync(Usuario usuario, CancellationToken cancellationToken = default);
}
