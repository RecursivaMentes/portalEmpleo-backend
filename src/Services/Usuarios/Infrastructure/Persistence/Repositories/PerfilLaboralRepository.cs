using System.Data;
using Application.PerfilLaboral;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Usuarios.Infrastructure;

namespace Usuarios.Infrastructure.Persistence.Repositories;

public class PerfilLaboralRepository(AppDbContext db) : IPerfilLaboralRepository
{
    public Task<PerfilLaboral?> ObtenerPorUsuarioIdAsync(
        Guid usuarioId, CancellationToken cancellationToken = default)
    {
        return db.PerfilesLaborales.FirstOrDefaultAsync(
            perfil => perfil.UsuarioId == usuarioId, cancellationToken);
    }

    public async Task AgregarAsync(
        PerfilLaboral perfil, CancellationToken cancellationToken = default)
    {
        await db.PerfilesLaborales.AddAsync(perfil, cancellationToken);
    }

    public async Task GuardarCambiosAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException postgres &&
            postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
            postgres.ConstraintName == "IX_perfiles_laborales_UsuarioId")
        {
            throw new DuplicateNameException("Ya existe un perfil laboral para tu cuenta. Actualizá la página y volvé a intentarlo.", ex);
        }
    }
}
