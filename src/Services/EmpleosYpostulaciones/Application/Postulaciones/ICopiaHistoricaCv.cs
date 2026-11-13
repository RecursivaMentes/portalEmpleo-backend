namespace Application.Postulaciones;

public sealed record CopiaHistoricaCv(byte[] Contenido, string Mime, string Nombre);

public interface ICopiaHistoricaCv
{
    Task<CopiaHistoricaCv> ObtenerAsync(Guid usuarioId, Guid cvId, CancellationToken ct = default);
}