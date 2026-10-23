using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Chats;
using Domain.Enums;

namespace Chatbot.Infrastructure.Ai;

/// <summary>Asistente que usa un modelo local a través de la API de Ollama (POST /api/chat).</summary>
public class OllamaAsistente(HttpClient http, OllamaOptions options) : IAsistenteIA
{
    public async Task<string> ResponderAsync(
        IReadOnlyList<MensajeIA> historial, string? contextoPerfil, CancellationToken cancellationToken)
    {
        var mensajes = new List<OllamaMensaje> { new("system", PromptAsistente.Construir(contextoPerfil)) };

        mensajes.AddRange(historial
            .Where(m => m.Rol is RolMensaje.Usuario or RolMensaje.Asistente)
            .Select(m => new OllamaMensaje(m.Rol == RolMensaje.Usuario ? "user" : "assistant", m.Contenido)));

        OllamaRespuesta? respuesta;
        try
        {
            // Se serializa a mano para que la petición lleve Content-Length y no use "chunked".
            var cuerpo = JsonSerializer.Serialize(new OllamaPeticion(options.Model, mensajes, Stream: false));
            using var contenido = new StringContent(cuerpo, Encoding.UTF8, "application/json");

            using var httpResponse = await http.PostAsync("api/chat", contenido, cancellationToken);

            httpResponse.EnsureSuccessStatusCode();

            respuesta = await httpResponse.Content.ReadFromJsonAsync<OllamaRespuesta>(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // TaskCanceledException sin cancelación del llamador = se agotó el tiempo de espera.
            throw new AsistenteNoDisponibleException(
                $"No se pudo obtener la respuesta de Ollama en {options.BaseUrl} (modelo '{options.Model}'). " +
                "Verifica que Ollama esté corriendo y que el modelo esté descargado.", ex);
        }

        var texto = respuesta?.Message?.Content?.Trim();

        if (string.IsNullOrEmpty(texto))
        {
            throw new AsistenteNoDisponibleException("Ollama devolvió una respuesta vacía.");
        }

        return texto;
    }

    private record OllamaMensaje(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private record OllamaPeticion(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] List<OllamaMensaje> Messages,
        [property: JsonPropertyName("stream")] bool Stream);

    private record OllamaRespuesta(
        [property: JsonPropertyName("message")] OllamaMensaje? Message);
}
