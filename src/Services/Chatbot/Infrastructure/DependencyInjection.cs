using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Application.Chats;
using Chatbot.Infrastructure.Ai;
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

        return services;
    }
}
