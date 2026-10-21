using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Application.Chats;
using Domain.Enums;

namespace Chatbot.Infrastructure.Ai;

public class ClaudeAsistente(AnthropicOptions options) : IAsistenteIA
{
    private const string PromptSistema = """
        Eres el asistente de un portal de empleos. Hablas siempre en español, con un tono cercano y claro, y respondes de forma breve.

        Tu trabajo es ayudar a la persona a:
        - Construir y completar su perfil laboral: datos personales, experiencia, formación, habilidades, idiomas y preferencias de trabajo (puestos, ubicaciones, modalidad y condiciones que quiere evitar).
        - Revisar y mejorar su CV y su carta de presentación.
        - Orientarse en su búsqueda de empleo.

        Reglas:
        - Haz una o dos preguntas por vez, no una lista larga.
        - No inventes datos de la persona, de empresas ni de ofertas laborales. Si no sabes algo, dilo.
        - Por ahora no tienes acceso al perfil guardado ni al listado de ofertas del portal: trabajas solo con lo que la persona te cuenta en esta conversación.
        - No pidas contraseñas ni datos bancarios.
        """;

    private readonly Lazy<AnthropicClient> _cliente = new(() => CrearCliente(options));

    public async Task<string> ResponderAsync(
        IReadOnlyList<MensajeIA> historial, CancellationToken cancellationToken)
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

        Message respuesta;
        try
        {
            respuesta = await cliente.Messages.Create(new MessageCreateParams
            {
                Model = options.Model,
                MaxTokens = options.MaxTokens,
                System = PromptSistema,
                // Conversación corta: esfuerzo bajo para responder rápido y gastar menos.
                OutputConfig = new OutputConfig { Effort = Effort.Low },
                Messages = mensajes,
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is AnthropicApiException or HttpRequestException)
        {
            throw new AsistenteNoDisponibleException(
                "No se pudo obtener la respuesta del asistente de IA.", ex);
        }

        if (respuesta.StopReason == "refusal")
        {
            return "No puedo ayudarte con eso. Si quieres, seguimos con tu perfil o tu búsqueda de empleo.";
        }

        var texto = string.Concat(
            respuesta.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text)).Trim();

        if (texto.Length == 0)
        {
            throw new AsistenteNoDisponibleException("El asistente de IA devolvió una respuesta vacía.");
        }

        return texto;
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
