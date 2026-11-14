namespace Recomendaciones.Application.Integrations;

public interface IUsuariosRecomendaciones
{
    Task<PerfilLaboralDto?> ObtenerPerfilAsync(string token, CancellationToken ct = default);
    Task<PerfilLaboralDto?> ObtenerPerfilInternoAsync(Guid usuarioId, string claveInterna, CancellationToken ct = default);
}

public interface IEmpleosRecomendaciones
{
    Task<List<EmpleoDto>> ObtenerEmpleosActivosAsync(CancellationToken ct = default);
}