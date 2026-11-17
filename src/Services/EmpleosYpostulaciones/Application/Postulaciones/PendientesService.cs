using Domain.Entities;
using Domain.Enums;

namespace Application.Postulaciones;

public sealed class PendientesService(
    IPendientesYPreparacionesRepository repository,
    IPostulacionRepository postulaciones)
{
    public async Task<EmpleoPendiente> AgregarAsync(Guid usuarioId, Guid empleoId, CancellationToken ct = default)
    {
        if (usuarioId == Guid.Empty || empleoId == Guid.Empty)
            throw PostulacionException.DatosInvalidos();

        var oferta = await postulaciones.ObtenerEmpleoAsync(empleoId, ct);
        if (oferta is null) throw PostulacionException.OfertaNoEncontrada();
        if (oferta.Estado != EstadoEmpleo.Publicado) throw PostulacionException.OfertaNoDisponible();
        if (oferta.FechaCierre is { } cierre && cierre <= DateTime.UtcNow)
            throw PostulacionException.OfertaFinalizada();

        if (await postulaciones.ExistePostulacionAsync(usuarioId, empleoId, ct))
            throw PostulacionException.Duplicada();
        if (await repository.ObtenerPendienteAsync(usuarioId, empleoId, ct) is not null)
            throw PostulacionException.PendienteDuplicado();

        var pendiente = new EmpleoPendiente
        {
            UsuarioId = usuarioId,
            EmpleoId = empleoId,
            FechaAgregado = DateTime.UtcNow
        };
        await repository.AgregarPendienteAsync(pendiente, ct);
        return pendiente;
    }

    public Task<List<EmpleoPendiente>> ListarAsync(Guid usuarioId, CancellationToken ct = default) =>
        repository.ListarPendientesAsync(usuarioId, ct);

    public Task<EmpleoPendiente?> ObtenerAsync(Guid usuarioId, Guid empleoId, CancellationToken ct = default) =>
        repository.ObtenerPendienteAsync(usuarioId, empleoId, ct);

    public async Task<bool> EliminarAsync(Guid usuarioId, Guid empleoId, CancellationToken ct = default)
    {
        if (usuarioId == Guid.Empty || empleoId == Guid.Empty)
            throw PostulacionException.DatosInvalidos();
        var pendiente = await repository.ObtenerPendienteAsync(usuarioId, empleoId, ct);
        if (pendiente is null) return false;
        await repository.EliminarPendienteAsync(pendiente, ct);
        return true;
    }
}
