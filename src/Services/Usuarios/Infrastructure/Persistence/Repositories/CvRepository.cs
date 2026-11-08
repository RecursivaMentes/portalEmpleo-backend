using Application.Cvs;
using Microsoft.EntityFrameworkCore;

namespace Usuarios.Infrastructure.Persistence.Repositories;

public sealed class CvRepository(AppDbContext db) : ICvRepository
{
    public Task<bool> PerteneceAUsuarioAsync(Guid cvId, Guid usuarioId, CancellationToken cancellationToken = default)
        => db.Cvs.AsNoTracking().AnyAsync(cv => cv.Id == cvId && cv.UsuarioId == usuarioId, cancellationToken);
}