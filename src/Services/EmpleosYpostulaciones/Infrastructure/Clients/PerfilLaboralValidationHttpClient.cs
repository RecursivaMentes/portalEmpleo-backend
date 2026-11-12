using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Application.Postulaciones;

namespace EmpleosYpostulaciones.Infrastructure.Clients;

public sealed class PerfilLaboralValidationHttpClient(HttpClient http) : IPerfilLaboralValidationClient
{
    public async Task<ValidacionPerfilLaboralResultado> ValidarAsync(
        string token, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/perfil-laboral/me/validacion");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, cancellationToken);

        // No existe perfil/cuenta activa: no permitir la postulacion.
        if (response.StatusCode == HttpStatusCode.NotFound)
            return new ValidacionPerfilLaboralResultado(false, ["Perfil laboral o cuenta activa"]);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Usuarios no pudo validar el perfil laboral (HTTP {(int)response.StatusCode}).");

        try
        {
            return await response.Content.ReadFromJsonAsync<ValidacionPerfilLaboralResultado>(
                cancellationToken: cancellationToken)
                ?? throw new HttpRequestException("Usuarios devolvio una validacion de perfil vacia.");
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new HttpRequestException("Usuarios devolvio una validacion de perfil invalida.", ex);
        }
    }
}
