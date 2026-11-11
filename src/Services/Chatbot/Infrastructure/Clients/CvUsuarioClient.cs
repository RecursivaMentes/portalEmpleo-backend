using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.Cvs;
using Microsoft.Extensions.Logging;

namespace Chatbot.Infrastructure.Clients;

/// <summary>Lee, crea y edita CV en el servicio Usuarios (api/cvs) reenviando el token de la persona.</summary>
public class CvUsuarioClient(HttpClient http, ILogger<CvUsuarioClient> logger) : ICvUsuarioClient
{
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public async Task<ResultadoListarCvs> ListarAsync(string accessToken, CancellationToken cancellationToken)
    {
        var r = await PedirAsync<List<CvUsuario>>(HttpMethod.Get, "api/cvs", accessToken, null, cancellationToken);
        return new ResultadoListarCvs(r.Dato, r.Mensaje);
    }

    public async Task<ResultadoObtenerCv> ObtenerAsync(string accessToken, Guid cvId, CancellationToken cancellationToken)
    {
        var r = await PedirAsync<CvUsuario>(HttpMethod.Get, $"api/cvs/{cvId}", accessToken, null, cancellationToken);
        return new ResultadoObtenerCv(r.Dato, r.NoEncontrado, r.Mensaje);
    }

    public async Task<ResultadoObtenerCv> ActualizarTextoAsync(
        string accessToken, Guid cvId, string textoPlano, long version, CancellationToken cancellationToken)
    {
        var r = await PedirAsync<CvUsuario>(
            HttpMethod.Put, $"api/cvs/{cvId}", accessToken, new { textoPlano, version }, cancellationToken);
        return new ResultadoObtenerCv(r.Dato, r.NoEncontrado, r.Mensaje);
    }

    public async Task<ResultadoCrearCv> CrearAsync(
        string accessToken, string titulo, string textoPlano, bool esPrincipal, CancellationToken cancellationToken)
    {
        var r = await PedirAsync<CvUsuario>(
            HttpMethod.Post, "api/cvs", accessToken, new { titulo, textoPlano, esPrincipal }, cancellationToken);
        return new ResultadoCrearCv(r.Dato, r.Mensaje);
    }

    private sealed record Respuesta<T>(T? Dato, bool NoEncontrado = false, string? Mensaje = null);

    private async Task<Respuesta<T>> PedirAsync<T>(
        HttpMethod metodo, string ruta, string accessToken, object? cuerpo, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(metodo, ruta);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            if (cuerpo is not null)
                request.Content = new StringContent(
                    JsonSerializer.Serialize(cuerpo, OpcionesJson), Encoding.UTF8, "application/json");

            using var response = await http.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new Respuesta<T>(default, NoEncontrado: true, Mensaje: "No encontramos ese CV.");

            if (!response.IsSuccessStatusCode)
            {
                // El cuerpo puede contener un CV (datos personales): se registra solo el estado.
                logger.LogWarning("Usuarios respondió {Status} en {Metodo} api/cvs.", (int)response.StatusCode, metodo);
                return new Respuesta<T>(default, Mensaje: await LectorErroresUsuarios.LeerMensajeAsync(response, cancellationToken));
            }

            var dato = await response.Content.ReadFromJsonAsync<T>(OpcionesJson, cancellationToken);
            return dato is null
                ? new Respuesta<T>(default, Mensaje: "El servicio de perfiles devolvió una respuesta vacía.")
                : new Respuesta<T>(dato);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                   && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "No se pudo comunicar con el servicio Usuarios ({Metodo} api/cvs).", metodo);
            return new Respuesta<T>(default, Mensaje: "No se pudo comunicar con el servicio de perfiles.");
        }
    }
}
