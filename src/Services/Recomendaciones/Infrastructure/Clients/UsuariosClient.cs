using System.Net;
using System.Net.Http.Headers;
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

    public async Task<PerfilLaboralDto?> ObtenerPerfilInternoAsync(
        Guid usuarioId, string claveInterna, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"/internal/usuarios/{usuarioId}/perfil-laboral");
            request.Headers.Add("X-Internal-Service", "Recomendaciones");
            request.Headers.Add("X-Internal-Key", claveInterna);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<PerfilLaboralDto>(
                cancellationToken: cancellationToken)
                ?? throw new JsonException("El perfil laboral interno no contiene datos.");
        }
        catch (HttpRequestException ex)
        {
            throw new ServicioExternoException("Usuarios", "Fallo la consulta interna de Usuarios.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ServicioExternoException("Usuarios", "Tiempo de espera agotado.", ex);
        }
        catch (JsonException ex)
        {
            throw new ServicioExternoException("Usuarios", "Perfil interno invalido.", ex);
        }
    }

    public async Task<PerfilLaboralDto?> ObtenerPerfilAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "/api/perfil-laboral/me"
            );
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.SendAsync(
                request,
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
