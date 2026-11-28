using Domain.Entities;

namespace Application.Postulaciones;

public interface IPendientesYPreparacionesRepository
{
    Task<EmpleoPendiente?> ObtenerPendienteAsync(Guid usuarioId, Guid empleoId, CancellationToken ct = default);
    Task<List<EmpleoPendiente>> ListarPendientesAsync(Guid usuarioId, CancellationToken ct = default);
    Task AgregarPendienteAsync(EmpleoPendiente pendiente, CancellationToken ct = default);
    Task EliminarPendienteAsync(EmpleoPendiente pendiente, CancellationToken ct = default);
    Task<List<Guid>> ListarDescartadasAsync(Guid usuarioId, CancellationToken ct = default) =>
        Task.FromResult(new List<Guid>());
    Task DescartarOfertaAsync(Guid usuarioId, Guid empleoId, CancellationToken ct = default) =>
        throw new NotSupportedException("El repositorio no admite exclusión explícita de ofertas.");
    Task RestaurarOfertaAsync(Guid usuarioId, Guid empleoId, CancellationToken ct = default) =>
        throw new NotSupportedException("El repositorio no admite restaurar ofertas.");

    async Task<PreparacionPostulacion?> ObtenerUltimoResultadoAsync(Guid usuarioId, CancellationToken ct = default) =>
        (await ListarPreparacionesAsync(usuarioId, ct)).Where(x =>
            x.Estado is Domain.Enums.EstadoPreparacionPostulacion.PendienteAprobacion or Domain.Enums.EstadoPreparacionPostulacion.PendienteAprobacionCv ||
            (x.Estado == Domain.Enums.EstadoPreparacionPostulacion.Enviada && x.PostulacionId.HasValue))
            .OrderByDescending(x => x.FechaActualizacion).FirstOrDefault();
    async Task<PreparacionPostulacion?> ObtenerUltimaPreparacionAsync(Guid usuarioId, CancellationToken ct = default) =>
        (await ListarPreparacionesAsync(usuarioId, ct)).OrderByDescending(x => x.FechaActualizacion).FirstOrDefault();
    Task<List<PreparacionPostulacion>> ListarPreparacionesAsync(Guid usuarioId, CancellationToken ct = default);
    Task<PreparacionPostulacion?> ObtenerPreparacionAsync(Guid id, Guid usuarioId, CancellationToken ct = default);
    Task<PreparacionPostulacion?> ObtenerEstadoVigenteAsync(Guid id, Guid usuarioId, CancellationToken ct = default) =>
        ObtenerPreparacionAsync(id, usuarioId, ct);
    Task<PreparacionPostulacion?> ObtenerActivaAsync(Guid usuarioId, CancellationToken ct = default);
    Task<bool> ExistePreparacionNoFinalizadaAsync(Guid usuarioId, Guid empleoId, CancellationToken ct = default);
    Task AgregarPreparacionAsync(PreparacionPostulacion preparacion, CancellationToken ct = default);
    Task GuardarPreparacionAsync(PreparacionPostulacion preparacion, CancellationToken ct = default);
    // El adaptador de producción hace la transición bajo los bloqueos de confirmación.
    Task GuardarCancelacionAsync(PreparacionPostulacion preparacion, CancellationToken ct = default) =>
        GuardarPreparacionAsync(preparacion, ct);
}
