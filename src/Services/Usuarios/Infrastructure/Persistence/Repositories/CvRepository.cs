using Application.Cvs;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

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

    public void Eliminar(Cv cv) => db.Cvs.Remove(cv);

    public Task GuardarCambiosAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
