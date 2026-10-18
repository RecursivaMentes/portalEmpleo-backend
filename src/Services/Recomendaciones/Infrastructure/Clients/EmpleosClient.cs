using System.Net.Http.Json;
using Recomendaciones.Application.Integrations;

namespace Recomendaciones.Infrastructure.Clients;

public class EmpleosClient
{
    private readonly HttpClient _httpClient;

    public EmpleosClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<EmpleoDto>> ObtenerEmpleosActivosAsync(
        CancellationToken cancellationToken = default)
    {
        var empleos = await _httpClient.GetFromJsonAsync<List<EmpleoDto>>(
            "/api/empleos/activos",
            cancellationToken
        );

        return empleos ?? new List<EmpleoDto>();
    }
}
