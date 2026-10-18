using System.Net;
using System.Net.Http.Json;
using Recomendaciones.Application.Integrations;

namespace Recomendaciones.Infrastructure.Clients;

public class UsuariosClient
{
    private readonly HttpClient _httpClient;

    public UsuariosClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PerfilLaboralDto?> ObtenerPerfilAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"/api/perfiles/{usuarioId}",
            cancellationToken
        );

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PerfilLaboralDto>(
            cancellationToken: cancellationToken
        );
    }
}
