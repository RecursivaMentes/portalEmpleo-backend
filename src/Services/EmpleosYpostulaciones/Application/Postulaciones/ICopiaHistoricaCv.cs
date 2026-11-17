namespace Application.Postulaciones;

public sealed record CopiaHistoricaCv(byte[] Contenido, string Mime, string Nombre);

public interface ICopiaHistoricaCv
{
    Task<CopiaHistoricaCv> ObtenerAsync(Guid usuarioId, Guid cvId, CancellationToken ct = default);
    Task<CopiaHistoricaCv> ObtenerVersionAsync(Guid usuarioId, Guid cvId,
        long? versionEsperada, CancellationToken ct = default) =>
        versionEsperada is null
            ? ObtenerAsync(usuarioId, cvId, ct)
            : throw new InvalidOperationException("El proveedor de CV debe validar la versión seleccionada.");
}