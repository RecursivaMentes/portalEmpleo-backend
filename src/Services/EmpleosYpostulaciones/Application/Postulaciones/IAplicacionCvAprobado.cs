namespace Application.Postulaciones;

public sealed record CvAplicado(Guid Id, long Version);
public interface IAplicacionCvAprobado
{
    Task<CvAplicado> AplicarAsync(Guid operacionId, Guid usuarioId, Guid? cvId, bool crearNuevo,
        string? titulo, string texto, long? versionOriginal, CancellationToken ct = default);
}