using System.Net.Http.Json;
using System.Text.Json;
using Recomendaciones.Application.Integrations;
using Recomendaciones.Infrastructure.Exceptions;

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
        try
        {
            using var response = await _httpClient.GetAsync(
                "/api/empleos/activos",
                cancellationToken
            );

            response.EnsureSuccessStatusCode();

            var empleos =
                await response.Content.ReadFromJsonAsync<List<EmpleoDto>>(
                    cancellationToken: cancellationToken
                );

            if (empleos is null)
            {
                throw new JsonException(
                    "El servicio de Empleos devolvió una respuesta vacía."
                );
            }

            return empleos;
        }
        catch (HttpRequestException ex)
        {
            throw new ServicioExternoException(
                "Empleos",
                "No fue posible comunicarse correctamente con el servicio de Empleos.",
                ex
            );
        }
        catch (TaskCanceledException ex)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new ServicioExternoException(
                "Empleos",
                "El servicio de Empleos superó el tiempo de espera.",
                ex
            );
        }
        catch (JsonException ex)
        {
            throw new ServicioExternoException(
                "Empleos",
                "El servicio de Empleos devolvió datos inválidos.",
                ex
            );
        }
    }
}