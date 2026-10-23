using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Application.Chats;
using Application.Perfil;
using Chatbot.Infrastructure.Ai;
using Chatbot.Infrastructure.Clients;
using Chatbot.Infrastructure.Persistence;
using Chatbot.Infrastructure.Persistence.Repositories;

namespace Chatbot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'DefaultConnection'.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IChatRepository, ChatRepository>();
        AddUsuarios(services, configuration);

        var proveedor = configuration["Asistente:Proveedor"];

        switch (string.IsNullOrWhiteSpace(proveedor) ? "Claude" : proveedor.Trim())
        {
            case var p when p.Equals("Claude", StringComparison.OrdinalIgnoreCase):
                AddClaude(services, configuration);
                break;

            case var p when p.Equals("Ollama", StringComparison.OrdinalIgnoreCase):
                AddOllama(services, configuration);
                break;

            default:
                throw new InvalidOperationException(
                    $"Asistente:Proveedor '{proveedor}' no es válido. Valores posibles: Claude, Ollama.");
        }

        return services;
    }

    private static void AddUsuarios(IServiceCollection services, IConfiguration configuration)
    {
        var baseUrl = configuration["Servicios:Usuarios:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = "http://localhost:5078";

        if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri))
            throw new InvalidOperationException($"Servicios:Usuarios:BaseUrl '{baseUrl}' no es una URL válida.");

        // Timeout corto: si Usuarios no responde pronto, el chat sigue sin el perfil.
        services.AddSingleton<IPerfilUsuarioClient>(sp => new UsuariosClient(
            new HttpClient { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(10) },
            sp.GetRequiredService<ILogger<UsuariosClient>>()));
    }

    private static void AddClaude(IServiceCollection services, IConfiguration configuration)
    {
        // La clave de Anthropic no se exige al arrancar: si falta, solo falla el envío de mensajes.
        var anthropic = new AnthropicOptions();
        if (!string.IsNullOrWhiteSpace(configuration["Anthropic:ApiKey"]))
            anthropic.ApiKey = configuration["Anthropic:ApiKey"];
        if (!string.IsNullOrWhiteSpace(configuration["Anthropic:Model"]))
            anthropic.Model = configuration["Anthropic:Model"]!;
        if (int.TryParse(configuration["Anthropic:MaxTokens"], out var maxTokens) && maxTokens > 0)
            anthropic.MaxTokens = maxTokens;

        services.AddSingleton(anthropic);
        services.AddSingleton<IAsistenteIA, ClaudeAsistente>();
    }

    private static void AddOllama(IServiceCollection services, IConfiguration configuration)
    {
        var ollama = new OllamaOptions();
        if (!string.IsNullOrWhiteSpace(configuration["Ollama:BaseUrl"]))
            ollama.BaseUrl = configuration["Ollama:BaseUrl"]!;
        if (!string.IsNullOrWhiteSpace(configuration["Ollama:Model"]))
            ollama.Model = configuration["Ollama:Model"]!;
        if (int.TryParse(configuration["Ollama:TimeoutSegundos"], out var timeout) && timeout > 0)
            ollama.TimeoutSegundos = timeout;

        if (!Uri.TryCreate(ollama.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri))
            throw new InvalidOperationException($"Ollama:BaseUrl '{ollama.BaseUrl}' no es una URL válida.");

        services.AddSingleton(ollama);
        services.AddSingleton<IAsistenteIA>(_ => new OllamaAsistente(
            new HttpClient { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(ollama.TimeoutSegundos) },
            ollama));
    }
}
