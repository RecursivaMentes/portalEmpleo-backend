using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Application.Chats;
using Application.Herramientas;
using Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Chatbot.Infrastructure.Ai;

public class ClaudeAsistente(AnthropicOptions options, ILogger<ClaudeAsistente> logger) : IAsistenteIA
{
    private const string MensajeRechazo =
        "No puedo ayudarte con eso. Si quieres, seguimos con tu perfil o tu búsqueda de empleo.";

    private const string MensajeSinTerminar =
        "Procesé tu mensaje, pero no pude terminar de responderte. Revisa tu perfil para ver qué quedó guardado.";

    private readonly Lazy<AnthropicClient> _cliente = new(() => CrearCliente(options));

    public async Task<string> ResponderAsync(
        IReadOnlyList<MensajeIA> historial,
        string? contextoPerfil,
        IEjecutorHerramientas? herramientas,
        CancellationToken cancellationToken)
    {
        var cliente = ObtenerCliente();

        var mensajes = historial
            .Where(m => m.Rol is RolMensaje.Usuario or RolMensaje.Asistente)
            .Select(m => new MessageParam
            {
                Role = m.Rol == RolMensaje.Usuario ? Role.User : Role.Assistant,
                Content = m.Contenido,
            })
            .ToList();

        var tools = herramientas?.Definiciones.Select(ToTool).ToList();
        var sistema = PromptAsistente.Construir(contextoPerfil, conHerramientas: herramientas is not null);

        string? ultimoTextoIntermedio = null;

        for (var ronda = 0; ; ronda++)
        {
            var respuesta = await CrearMensajeAsync(cliente, sistema, mensajes, tools, cancellationToken);

            if (respuesta.StopReason == "refusal") return MensajeRechazo;

            var texto = string.Concat(
                respuesta.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text)).Trim();

            var usos = respuesta.Content.Select(b => b.Value).OfType<ToolUseBlock>().ToList();

            // Sin pedidos de herramientas, la respuesta de texto es la final.
            if (usos.Count == 0 || herramientas is null)
            {
                if (texto.Length == 0) texto = ultimoTextoIntermedio ?? "";
                if (texto.Length == 0)
                    throw new AsistenteNoDisponibleException("El asistente de IA devolvió una respuesta vacía.");

                return texto;
            }

            if (texto.Length > 0) ultimoTextoIntermedio = texto;

            // Tope de seguridad: el modelo sigue pidiendo herramientas sin llegar a responder.
            if (ronda >= LimitesHerramientas.MaxRondas)
            {
                logger.LogWarning("El modelo superó el máximo de {Max} rondas de herramientas.", LimitesHerramientas.MaxRondas);
                return ultimoTextoIntermedio ?? MensajeSinTerminar;
            }

            // La respuesta del modelo se devuelve tal cual (incluido su razonamiento, con la firma) y
            // se agrega un resultado por cada herramienta pedida: la API rechaza un tool_use sin resultado.
            List<ContentBlockParam> eco = [];
            List<ContentBlockParam> resultados = [];

            foreach (var bloque in respuesta.Content)
            {
                if (bloque.TryPickText(out TextBlock? t))
                {
                    eco.Add(new TextBlockParam { Text = t.Text });
                }
                else if (bloque.TryPickThinking(out ThinkingBlock? pensamiento))
                {
                    eco.Add(new ThinkingBlockParam { Thinking = pensamiento.Thinking, Signature = pensamiento.Signature });
                }
                else if (bloque.TryPickRedactedThinking(out RedactedThinkingBlock? oculto))
                {
                    eco.Add(new RedactedThinkingBlockParam { Data = oculto.Data });
                }
                else if (bloque.TryPickToolUse(out ToolUseBlock? uso))
                {
                    eco.Add(new ToolUseBlockParam { ID = uso.ID, Name = uso.Name, Input = uso.Input });

                    var resultado = await EjecutarHerramientaAsync(herramientas, uso, cancellationToken);

                    resultados.Add(new ToolResultBlockParam
                    {
                        ToolUseID = uso.ID,
                        Content = resultado.Texto,
                        IsError = resultado.EsError,
                    });
                }
            }

            mensajes.Add(new MessageParam { Role = Role.Assistant, Content = eco });
            mensajes.Add(new MessageParam { Role = Role.User, Content = resultados });
        }
    }

    private async Task<Message> CrearMensajeAsync(
        AnthropicClient cliente,
        string sistema,
        List<MessageParam> mensajes,
        List<ToolUnion>? tools,
        CancellationToken cancellationToken)
    {
        try
        {
            return await cliente.Messages.Create(new MessageCreateParams
            {
                Model = options.Model,
                MaxTokens = options.MaxTokens,
                System = sistema,
                // Conversación corta: esfuerzo bajo para responder rápido y gastar menos.
                OutputConfig = new OutputConfig { Effort = Effort.Low },
                Messages = mensajes,
                Tools = tools,
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is AnthropicApiException or HttpRequestException)
        {
            throw new AsistenteNoDisponibleException(
                "No se pudo obtener la respuesta del asistente de IA.", ex);
        }
    }

    private async Task<ResultadoHerramienta> EjecutarHerramientaAsync(
        IEjecutorHerramientas herramientas, ToolUseBlock uso, CancellationToken cancellationToken)
    {
        try
        {
            var argumentos = JsonSerializer.Serialize(uso.Input);
            return await herramientas.EjecutarAsync(new LlamadaHerramienta(uso.Name, argumentos), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Un error inesperado de una herramienta no debe tirar toda la conversación.
            logger.LogError(ex, "Falló la herramienta {Herramienta}.", uso.Name);
            return new ResultadoHerramienta("Ocurrió un error inesperado al ejecutar la herramienta.", EsError: true);
        }
    }

    /// <summary>Convierte la definición neutral (JSON Schema) al formato de herramienta de Anthropic.</summary>
    private static ToolUnion ToTool(DefinicionHerramienta definicion)
    {
        using var doc = JsonDocument.Parse(definicion.EsquemaJson);

        var propiedades = new Dictionary<string, JsonElement>();
        foreach (var propiedad in doc.RootElement.GetProperty("properties").EnumerateObject())
            propiedades[propiedad.Name] = propiedad.Value.Clone();

        var obligatorios = doc.RootElement.GetProperty("required")
            .EnumerateArray().Select(e => e.GetString()!).ToList();

        return new Tool
        {
            Name = definicion.Nombre,
            Description = definicion.Descripcion,
            InputSchema = new() { Properties = propiedades, Required = obligatorios },
        };
    }

    private AnthropicClient ObtenerCliente()
    {
        try
        {
            return _cliente.Value;
        }
        catch (InvalidOperationException ex)
        {
            throw new AsistenteNoDisponibleException(ex.Message, ex);
        }
    }

    private static AnthropicClient CrearCliente(AnthropicOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return new AnthropicClient { ApiKey = options.ApiKey };
        }

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
        {
            throw new InvalidOperationException(
                "Falta la clave de la API de Anthropic (Anthropic:ApiKey o ANTHROPIC_API_KEY).");
        }

        return new AnthropicClient();
    }
}
