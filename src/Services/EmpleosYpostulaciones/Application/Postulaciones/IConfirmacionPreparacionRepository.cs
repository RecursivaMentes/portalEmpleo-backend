using Domain.Entities;

namespace Application.Postulaciones;

public interface IConfirmacionPreparacionRepository
{
    // Revalida estado, version, oferta y duplicados dentro de una transaccion.
    Task<Postulacion> ConfirmarEnvioAsync(Guid usuarioId, Guid preparacionId,
        int versionValidada, Postulacion postulacion, CancellationToken ct = default);
    Task<Postulacion> ConfirmarEnvioConCupoAsync(Guid usuarioId, Guid preparacionId,
        int versionValidada, Postulacion postulacion, int limite, DateTime inicioUtc,
        DateTime finUtc, CancellationToken ct = default) =>
        throw new NotSupportedException("El repositorio no implementa confirmacion con cupo.");
}
