using Domain.Entities;
using Domain.Enums;

namespace Application.Postulaciones;

public sealed class PreparacionesService(
    IPendientesYPreparacionesRepository repository,
    IPostulacionRepository postulaciones)
{
    // Se invocara desde el motor del Commit 8. No se expone POST hasta tener
    // un procesador que pueda completar o liberar la ejecucion reservada.
    public async Task<PreparacionPostulacion> RegistrarInicioAsync(
        Guid usuarioId, Guid empleoId, OrigenPreparacionPostulacion origen,
        CancellationToken ct = default)
    {
        if (usuarioId == Guid.Empty || empleoId == Guid.Empty || !Enum.IsDefined(origen))
            throw PostulacionException.DatosInvalidos();
        var oferta = await postulaciones.ObtenerEmpleoAsync(empleoId, ct);
        if (oferta is null) throw PostulacionException.OfertaNoEncontrada();
        if (oferta.Estado != EstadoEmpleo.Publicado) throw PostulacionException.OfertaNoDisponible();
        if (oferta.FechaCierre is { } cierre && cierre <= DateTime.UtcNow)
            throw PostulacionException.OfertaFinalizada();
        if (await postulaciones.ExistePostulacionAsync(usuarioId, empleoId, ct))
            throw PostulacionException.Duplicada();
        if (await repository.ObtenerActivaAsync(usuarioId, ct) is not null)
            throw PostulacionException.IaOcupada();
        if (await repository.ExistePreparacionNoFinalizadaAsync(usuarioId, empleoId, ct))
            throw PostulacionException.PreparacionDuplicada();

        var ahora = DateTime.UtcNow;
        var preparacion = new PreparacionPostulacion
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            EmpleoId = empleoId,
            Origen = origen,
            Estado = EstadoPreparacionPostulacion.Preparando,
            FechaCreacion = ahora,
            FechaActualizacion = ahora
        };
        // Los indices unicos de PostgreSQL cierran la carrera entre peticiones simultaneas.
        await repository.AgregarPreparacionAsync(preparacion, ct);
        return preparacion;
    }

    public Task<List<PreparacionPostulacion>> ListarAsync(Guid usuarioId, CancellationToken ct = default) =>
        repository.ListarPreparacionesAsync(usuarioId, ct);

    public Task<PreparacionPostulacion?> ObtenerAsync(Guid id, Guid usuarioId, CancellationToken ct = default) =>
        repository.ObtenerPreparacionAsync(id, usuarioId, ct);

    public async Task<(bool Disponible, PreparacionPostulacion? Preparacion)> DisponibilidadAsync(
        Guid usuarioId, CancellationToken ct = default)
    {
        var activa = await repository.ObtenerActivaAsync(usuarioId, ct);
        return (activa is null, activa);
    }

    public async Task<PreparacionPostulacion?> CancelarAsync(
        Guid id, Guid usuarioId, CancellationToken ct = default)
    {
        if (id == Guid.Empty || usuarioId == Guid.Empty)
            throw PostulacionException.DatosInvalidos();
        var preparacion = await repository.ObtenerPreparacionAsync(id, usuarioId, ct);
        if (preparacion is null) return null;

        switch (preparacion.Estado)
        {
            case EstadoPreparacionPostulacion.Preparando:
            case EstadoPreparacionPostulacion.EnModificacion:
                // Se libera la ejecucion solo cuando el trabajador confirme su detencion.
                preparacion.Estado = EstadoPreparacionPostulacion.CancelacionSolicitada;
                break;
            case EstadoPreparacionPostulacion.CancelacionSolicitada:
            case EstadoPreparacionPostulacion.Cancelada:
                return preparacion;
            case EstadoPreparacionPostulacion.PendienteAprobacion:
            case EstadoPreparacionPostulacion.PendienteAprobacionCv:
                preparacion.Estado = EstadoPreparacionPostulacion.Cancelada;
                break;
            default:
                // Enviando no se cancela a ciegas; Enviada/Fallida no se reescriben.
                throw PostulacionException.PreparacionNoCancelable();
        }
        preparacion.FechaActualizacion = DateTime.UtcNow;
        preparacion.VersionEdicion++;
        await repository.GuardarPreparacionAsync(preparacion, ct);
        return preparacion;
    }
}
