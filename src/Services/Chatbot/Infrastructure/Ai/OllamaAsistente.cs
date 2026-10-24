using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Application.Chats;
using Application.Herramientas;
using Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Chatbot.Infrastructure.Ai;

/// <summary>Asistente que usa un modelo local a través de la API de Ollama (POST /api/chat).</summary>
public class OllamaAsistente(HttpClient http, OllamaOptions options, ILogger<OllamaAsistente> logger) : IAsistenteIA
{
    private const string MensajeSinTerminar =
        "Procesé tu mensaje, pero no pude terminar de responderte. Revisa tu perfil para ver qué quedó guardado.";

    public async Task<string> ResponderAsync(
        IReadOnlyList<MensajeIA> historial,
        string? contextoPerfil,
        IEjecutorHerramientas? herramientas,
        CancellationToken cancellationToken)
    {
        var mensajes = new List<OllamaMensaje> { Sistema(contextoPerfil, herramientas is not null) };

        mensajes.AddRange(historial
            .Where(m => m.Rol is RolMensaje.Usuario or RolMensaje.Asistente)
            .Select(m => new OllamaMensaje(m.Rol == RolMensaje.Usuario ? "user" : "assistant", m.Contenido)));

        var tools = herramientas?.Definiciones.Select(ToHerramienta).ToList();
        string? ultimoTextoIntermedio = null;

        for (var ronda = 0; ; ronda++)
        {
            OllamaMensaje? respuesta;
            try
            {
                respuesta = await EnviarAsync(mensajes, tools, cancellationToken);
            }
            catch (HerramientasNoSoportadasException)
            {
                // El modelo no admite herramientas: se sigue sin ellas, con las instrucciones de siempre.
                logger.LogWarning("El modelo '{Modelo}' de Ollama no soporta herramientas; se responde sin ellas.", options.Model);
                herramientas = null;
                tools = null;
                mensajes[0] = Sistema(contextoPerfil, conHerramientas: false);
                respuesta = await EnviarAsync(mensajes, tools, cancellationToken);
            }

            var texto = respuesta?.Content?.Trim() ?? "";
            var llamadas = respuesta?.ToolCalls ?? [];

            if (llamadas.Count == 0 || herramientas is null)
            {
                if (texto.Length == 0) texto = ultimoTextoIntermedio ?? "";
                if (texto.Length == 0)
                    throw new AsistenteNoDisponibleException("Ollama devolvió una respuesta vacía.");

                return texto;
            }

            if (texto.Length > 0) ultimoTextoIntermedio = texto;

            if (ronda >= LimitesHerramientas.MaxRondas)
            {
                logger.LogWarning("El modelo superó el máximo de {Max} rondas de herramientas.", LimitesHerramientas.MaxRondas);
                return ultimoTextoIntermedio ?? MensajeSinTerminar;
            }

            // El mensaje del modelo se devuelve tal cual, y luego un resultado por cada herramienta que pidió.
            mensajes.Add(new OllamaMensaje("assistant", respuesta!.Content ?? "", llamadas.Select(Normalizar).ToList()));

            foreach (var llamada in llamadas)
            {
                var resultado = await EjecutarHerramientaAsync(herramientas, llamada, cancellationToken);
                var contenido = resultado.EsError ? "ERROR: " + resultado.Texto : resultado.Texto;

                mensajes.Add(new OllamaMensaje("tool", contenido, ToolName: llamada.Function.Name));
            }
        }
    }

    private async Task<OllamaMensaje?> EnviarAsync(
        List<OllamaMensaje> mensajes, List<OllamaHerramienta>? tools, CancellationToken cancellationToken)
    {
        try
        {
            // Se serializa a mano para que la petición lleve Content-Length y no use "chunked".
            var cuerpo = JsonSerializer.Serialize(new OllamaPeticion(options.Model, mensajes, Stream: false, tools));
            using var contenido = new StringContent(cuerpo, Encoding.UTF8, "application/json");

            using var httpResponse = await http.PostAsync("api/chat", contenido, cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var detalle = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

                if (httpResponse.StatusCode == HttpStatusCode.BadRequest && tools is not null
                    && detalle.Contains("tool", StringComparison.OrdinalIgnoreCase))
                {
                    throw new HerramientasNoSoportadasException();
                }

                throw new HttpRequestException($"Ollama respondió {(int)httpResponse.StatusCode}.", null, httpResponse.StatusCode);
            }

            var json = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<OllamaRespuesta>(json)?.Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                   && !cancellationToken.IsCancellationRequested)
        {
            // TaskCanceledException sin cancelación del llamador = se agotó el tiempo de espera.
            throw new AsistenteNoDisponibleException(
                $"No se pudo obtener la respuesta de Ollama en {options.BaseUrl} (modelo '{options.Model}'). " +
                "Verifica que Ollama esté corriendo y que el modelo esté descargado.", ex);
        }
    }

    private async Task<ResultadoHerramienta> EjecutarHerramientaAsync(
        IEjecutorHerramientas herramientas, OllamaLlamada llamada, CancellationToken cancellationToken)
    {
        try
        {
            var argumentos = llamada.Function.Arguments.ValueKind == JsonValueKind.Object
                ? llamada.Function.Arguments.GetRawText()
                : "{}";

            return await herramientas.EjecutarAsync(
                new LlamadaHerramienta(llamada.Function.Name, argumentos), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Un error inesperado de una herramienta no debe tirar toda la conversación.
            logger.LogError(ex, "Falló la herramienta {Herramienta}.", llamada.Function.Name);
            return new ResultadoHerramienta("Ocurrió un error inesperado al ejecutar la herramienta.", EsError: true);
        }
    }

    private static OllamaMensaje Sistema(string? contextoPerfil, bool conHerramientas) =>
        new("system", PromptAsistente.Construir(contextoPerfil, conHerramientas));

    private static OllamaHerramienta ToHerramienta(DefinicionHerramienta definicion) =>
        new("function", new OllamaDefinicion(
            definicion.Nombre, definicion.Descripcion, JsonNode.Parse(definicion.EsquemaJson)!));

    /// <summary>Al devolver la llamada al modelo, los argumentos nunca pueden faltar.</summary>
    private static OllamaLlamada Normalizar(OllamaLlamada llamada) =>
        llamada.Function.Arguments.ValueKind == JsonValueKind.Object
            ? llamada
            : llamada with { Function = llamada.Function with { Arguments = JsonDocument.Parse("{}").RootElement.Clone() } };

    private sealed class HerramientasNoSoportadasException : Exception;

    private record OllamaMensaje(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string? Content,
        [property: JsonPropertyName("tool_calls"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        List<OllamaLlamada>? ToolCalls = null,
        [property: JsonPropertyName("tool_name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? ToolName = null);

    private record OllamaLlamada(
        [property: JsonPropertyName("function")] OllamaFuncion Function);

    private record OllamaFuncion(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("arguments")] JsonElement Arguments);

    private record OllamaHerramienta(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("function")] OllamaDefinicion Function);

    private record OllamaDefinicion(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("parameters")] JsonNode Parameters);

    private record OllamaPeticion(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] List<OllamaMensaje> Messages,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("tools"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        List<OllamaHerramienta>? Tools);

    private record OllamaRespuesta(
        [property: JsonPropertyName("message")] OllamaMensaje? Message);
}
