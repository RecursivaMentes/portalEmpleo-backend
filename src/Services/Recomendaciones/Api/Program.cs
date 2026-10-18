using Recomendaciones.Application.Services;
using Recomendaciones.Api.Endpoints;
using Recomendaciones.Infrastructure.Clients;
using Recomendaciones.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddScoped<CompatibilidadService>();
builder.Services.AddScoped<AnalisisOfertasService>();

builder.Services.AddHttpClient<UsuariosClient>(client =>
{
    var url = builder.Configuration["Microservices:UsuariosUrl"]
        ?? throw new InvalidOperationException(
            "Falta configurar Microservices:UsuariosUrl");

    client.BaseAddress = new Uri(url);
});

builder.Services.AddHttpClient<EmpleosClient>(client =>
{
    var url = builder.Configuration["Microservices:EmpleosUrl"]
        ?? throw new InvalidOperationException(
            "Falta configurar Microservices:EmpleosUrl");

    client.BaseAddress = new Uri(url);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapRecomendacionesEndpoints();

app.Run();
