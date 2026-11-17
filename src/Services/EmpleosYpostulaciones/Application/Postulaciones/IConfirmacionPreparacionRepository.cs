using Domain.Entities;

namespace Application.Postulaciones;

public interface IConfirmacionPreparacionRepository
{
    // Sólo para envíos sin aprobación del usuario: revalida el modo en la transacción.
    Task<Postulacion> ConfirmarEnvioAutomaticoAsync(Guid usuarioId, Guid preparacionId,
        int versionValidada, Postulacion postulacion, CancellationToken ct = default) =>
        ConfirmarEnvioAsync(usuarioId, preparacionId, versionValidada, postulacion, ct);

    // Revalida estado, version, oferta y duplicados dentro de una transaccion.
    Task<Postulacion> ConfirmarEnvioAsync(Guid usuarioId, Guid preparacionId,
        int versionValidada, Postulacion postulacion, CancellationToken ct = default);
    Task<Postulacion> ConfirmarEnvioConCupoAsync(Guid usuarioId, Guid preparacionId,
        int versionValidada, Postulacion postulacion, int limite, DateTime inicioUtc,
        DateTime finUtc, CancellationToken ct = default) =>
        throw new NotSupportedException("El repositorio no implementa confirmacion con cupo.");
}
