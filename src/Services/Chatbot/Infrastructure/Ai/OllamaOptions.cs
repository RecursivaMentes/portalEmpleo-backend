namespace Chatbot.Infrastructure.Ai;

public class OllamaOptions
{
    /// <summary>Dirección del servidor de Ollama. Por defecto escucha en el puerto 11434.</summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>Modelo ya descargado con <c>ollama pull</c>.</summary>
    public string Model { get; set; } = "llama3.2";

    /// <summary>Los modelos locales pueden tardar, sobre todo la primera vez que se cargan en memoria.</summary>
    public int TimeoutSegundos { get; set; } = 180;
}
