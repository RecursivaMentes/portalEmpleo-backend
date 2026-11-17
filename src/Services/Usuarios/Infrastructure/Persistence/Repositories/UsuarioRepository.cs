using Npgsql;
using System.Data;
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
        var emailNormalizado = email.Trim().ToLowerInvariant();
        return db.Usuarios.FirstOrDefaultAsync(
            x => x.Email.ToLower() == emailNormalizado,
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

    public async Task AddAsync(
        Usuario usuario,
        CancellationToken cancellationToken = default)
    {
        await db.Usuarios.AddAsync(usuario, cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException postgres &&
            postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
            postgres.ConstraintName == "IX_usuarios_Email")
        {
            throw new DuplicateNameException("El correo ya existe.", ex);
        }
    }

    public Task ActualizarPreferenciasLaboralesAsync(Guid usuarioId, string preferenciasJson,
        CancellationToken cancellationToken = default) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE usuarios SET \"PreferenciasJson\" = jsonb_set(COALESCE(\"PreferenciasJson\", '{{}}'::jsonb), '{{preferenciasLaborales}}', CAST({preferenciasJson} AS jsonb), true) WHERE \"Id\" = {usuarioId} AND \"Estado\" = TRUE",
            cancellationToken);

    public async Task UpdateAsync(
        Usuario usuario,
        CancellationToken cancellationToken = default)
    {
        // GetByIdAsync obtiene una entidad con seguimiento de EF Core.
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UpdatePasswordIfUnchangedAsync(
        Guid userId,
        string currentPasswordHash,
        string newPasswordHash,
        CancellationToken cancellationToken = default)
    {
        var updatedRows = await db.Usuarios
            .Where(u =>
                u.Id == userId &&
                u.PasswordHash == currentPasswordHash &&
                u.Estado)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    u => u.PasswordHash,
                    newPasswordHash),
                cancellationToken);

        return updatedRows == 1;
    }
}
