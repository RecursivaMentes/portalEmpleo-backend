using Application.Auth;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class UsuarioRepository(AppDbContext db) : IUsuarioRepository
{
    public Task<Usuario?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        return db.Usuarios
            .FirstOrDefaultAsync(
                x => x.Email == email,
                cancellationToken);
    }
}
