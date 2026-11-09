using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.Cvs;
using Microsoft.Extensions.Logging;

namespace Chatbot.Infrastructure.Clients;

/// <summary>Crea CV en el servicio Usuarios (POST api/cvs) reenviando el token de la persona.</summary>
public class CvUsuarioClient(HttpClient http, ILogger<CvUsuarioClient> logger) : ICvUsuarioClient
{
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public async Task<ResultadoCrearCv> CrearAsync(
        string accessToken, string titulo, string textoPlano, bool esPrincipal, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/cvs")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { titulo, textoPlano, esPrincipal }, OpcionesJson),
                    Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await http.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var cv = await response.Content.ReadFromJsonAsync<CvCreado>(OpcionesJson, cancellationToken);
                return cv is null
                    ? new ResultadoCrearCv(null, "El servicio de perfiles devolvió una respuesta vacía.")
                    : new ResultadoCrearCv(cv);
            }

            // El cuerpo contiene un CV (datos personales): se registra solo el estado, nunca el contenido.
            logger.LogWarning("Usuarios respondió {Status} en POST api/cvs.", (int)response.StatusCode);
            return new ResultadoCrearCv(null, await LectorErroresUsuarios.LeerMensajeAsync(response, cancellationToken));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                   && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "No se pudo crear el CV en el servicio Usuarios.");
            return new ResultadoCrearCv(null, "No se pudo comunicar con el servicio de perfiles.");
        }
    }
}
