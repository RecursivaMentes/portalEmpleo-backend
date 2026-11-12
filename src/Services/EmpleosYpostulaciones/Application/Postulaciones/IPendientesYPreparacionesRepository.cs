using Domain.Entities;

namespace Application.Postulaciones;

public interface IPendientesYPreparacionesRepository
{
    Task<EmpleoPendiente?> ObtenerPendienteAsync(Guid usuarioId, Guid empleoId, CancellationToken ct = default);
    Task<List<EmpleoPendiente>> ListarPendientesAsync(Guid usuarioId, CancellationToken ct = default);
    Task AgregarPendienteAsync(EmpleoPendiente pendiente, CancellationToken ct = default);
    Task EliminarPendienteAsync(EmpleoPendiente pendiente, CancellationToken ct = default);

    Task<List<PreparacionPostulacion>> ListarPreparacionesAsync(Guid usuarioId, CancellationToken ct = default);
    Task<PreparacionPostulacion?> ObtenerPreparacionAsync(Guid id, Guid usuarioId, CancellationToken ct = default);
    Task<PreparacionPostulacion?> ObtenerActivaAsync(Guid usuarioId, CancellationToken ct = default);
    Task<bool> ExistePreparacionNoFinalizadaAsync(Guid usuarioId, Guid empleoId, CancellationToken ct = default);
    Task AgregarPreparacionAsync(PreparacionPostulacion preparacion, CancellationToken ct = default);
    Task GuardarPreparacionAsync(PreparacionPostulacion preparacion, CancellationToken ct = default);
}
