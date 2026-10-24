using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Recomendaciones.Application.Integrations;
using Recomendaciones.Infrastructure.Exceptions;

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
        try
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

            var perfil =
                await response.Content.ReadFromJsonAsync<PerfilLaboralDto>(
                    cancellationToken: cancellationToken
                );

            if (perfil is null)
            {
                throw new JsonException(
                    "El servicio de Usuarios devolvió un perfil vacío."
                );
            }

            return perfil;
        }
        catch (HttpRequestException ex)
        {
            throw new ServicioExternoException(
                "Usuarios",
                "No fue posible comunicarse correctamente con el servicio de Usuarios.",
                ex
            );
        }
        catch (TaskCanceledException ex)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new ServicioExternoException(
                "Usuarios",
                "El servicio de Usuarios superó el tiempo de espera.",
                ex
            );
        }
        catch (JsonException ex)
        {
            throw new ServicioExternoException(
                "Usuarios",
                "El servicio de Usuarios devolvió datos inválidos.",
                ex
            );
        }
    }
}