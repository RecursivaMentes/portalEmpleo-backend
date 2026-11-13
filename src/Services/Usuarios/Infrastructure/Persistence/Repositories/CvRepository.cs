using Application.Cvs;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using Npgsql;

namespace Usuarios.Infrastructure.Persistence.Repositories;

public sealed class CvRepository(AppDbContext db) : ICvRepository
{
    public Task<bool> PerteneceAUsuarioAsync(Guid cvId, Guid usuarioId, CancellationToken cancellationToken = default)
        => db.Cvs.AsNoTracking().AnyAsync(cv => cv.Id == cvId && cv.UsuarioId == usuarioId, cancellationToken);

    public Task<List<Cv>> ListarPorUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default)
        => db.Cvs
            .Where(cv => cv.UsuarioId == usuarioId)
            .OrderByDescending(cv => cv.FechaActualizacion)
            .ToListAsync(cancellationToken);

    public Task<Cv?> ObtenerAsync(Guid cvId, Guid usuarioId, CancellationToken cancellationToken = default)
        => db.Cvs.FirstOrDefaultAsync(cv => cv.Id == cvId && cv.UsuarioId == usuarioId, cancellationToken);

    public async Task AgregarAsync(Cv cv, CancellationToken cancellationToken = default)
        => await db.Cvs.AddAsync(cv, cancellationToken);

    public Task<OperacionCvIA?> ObtenerOperacionCvIAAsync(Guid operacionId, Guid usuarioId, CancellationToken ct = default) =>
        db.OperacionesCvIA.AsNoTracking().FirstOrDefaultAsync(x => x.Id == operacionId && x.UsuarioId == usuarioId, ct);

    public async Task RegistrarOperacionCvIAAsync(OperacionCvIA operacion, CancellationToken ct = default) =>
        await db.OperacionesCvIA.AddAsync(operacion, ct);

    public async Task<T> EjecutarExclusivoAsync<T>(Guid usuarioId, Func<Task<T>> accion, CancellationToken cancellationToken = default)
    {
        await using var transaccion = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var comando = db.Database.GetDbConnection().CreateCommand();
            comando.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
            comando.CommandText = "SELECT \"Id\" FROM usuarios WHERE \"Id\" = @usuario FOR UPDATE NOWAIT";
            var parametro = comando.CreateParameter();
            parametro.ParameterName = "usuario";
            parametro.Value = usuarioId;
            comando.Parameters.Add(parametro);
            if (await comando.ExecuteScalarAsync(cancellationToken) is null)
                throw new InvalidOperationException("Usuario no encontrado para proteger sus CV.");

            var resultado = await accion();
            await transaccion.CommitAsync(cancellationToken);
            return resultado;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            throw new CvConflictoException("Otra operacion sobre tus CV esta en curso. Actualiza la informacion y volve a intentarlo.");
        }
    }

    public void Eliminar(Cv cv) => db.Cvs.Remove(cv);

    public async Task GuardarCambiosAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entrada in db.ChangeTracker.Entries<Cv>().Where(e => e.State == EntityState.Modified))
            entrada.Entity.Version++;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new CvConflictoException("El CV fue modificado por otra operacion. Actualiza la informacion y volve a intentarlo.");
        }
    }
}
