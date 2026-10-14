using Chatbot.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Configuración nativa de .NET 10 para documentación de APIs
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();