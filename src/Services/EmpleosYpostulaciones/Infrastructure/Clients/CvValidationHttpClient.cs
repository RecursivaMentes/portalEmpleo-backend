using System.Net;
using System.Net.Http.Headers;
using Application.Postulaciones;

namespace EmpleosYpostulaciones.Infrastructure.Clients;

public sealed class CvValidationHttpClient(HttpClient http) : ICvValidationClient
{
    public async Task<bool> PerteneceAlUsuarioAsync(Guid cvId, string token, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/cvs/{cvId}/validacion");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent) return true;
        if (response.StatusCode == HttpStatusCode.NotFound) return false;

        // Si Usuarios no puede validar, nunca aceptar la postulacion por defecto.
        throw new HttpRequestException($"Usuarios no pudo validar el CV (HTTP {(int)response.StatusCode}).");
    }
}