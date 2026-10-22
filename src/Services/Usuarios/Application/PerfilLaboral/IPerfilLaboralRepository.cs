using PerfilLaboralEntity = Domain.Entities.PerfilLaboral;

namespace Application.PerfilLaboral;

public interface IPerfilLaboralRepository
{
    Task<PerfilLaboralEntity?> ObtenerPorUsuarioIdAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default);

    Task AgregarAsync(
        PerfilLaboralEntity perfil,
        CancellationToken cancellationToken = default);

    Task GuardarCambiosAsync(
        CancellationToken cancellationToken = default);
}
