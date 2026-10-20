using Application.Auth;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Usuarios.Infrastructure;

namespace Usuarios.Infrastructure.Persistence.Repositories;

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

    public Task<Usuario?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return db.Usuarios
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public async Task AddAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        await db.Usuarios.AddAsync(usuario, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        db.Usuarios.Update(usuario);
        await db.SaveChangesAsync(cancellationToken);
    }

}
