using Domain.Entities;

namespace Application.Postulaciones;

public interface IConfirmacionPreparacionRepository
{
    // Revalida estado, version, oferta y duplicados dentro de una transaccion.
    Task<Postulacion> ConfirmarEnvioAsync(Guid usuarioId, Guid preparacionId,
        int versionValidada, Postulacion postulacion, CancellationToken ct = default);
}
