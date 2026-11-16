namespace Chatbot.Infrastructure.Ai;

public class AnthropicOptions
{
    /// <summary>Clave de la API. Si está vacía se usa la variable de entorno ANTHROPIC_API_KEY.</summary>
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "claude-opus-5-5";

    public int MaxTokens { get; set; } = 4096;
}
