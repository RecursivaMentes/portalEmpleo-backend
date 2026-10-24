using Recomendaciones.Application.Services;
using Recomendaciones.Api.Endpoints;
using Recomendaciones.Api.Exceptions;
using Recomendaciones.Infrastructure.Clients;
using Recomendaciones.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddExceptionHandler<RecomendacionesExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddScoped<CompatibilidadService>();
builder.Services.AddScoped<AnalisisOfertasService>();
builder.Services.AddScoped<RecomendacionesService>();

builder.Services.AddHttpClient<UsuariosClient>(client =>
{
    var url = builder.Configuration["Microservices:UsuariosUrl"]
        ?? throw new InvalidOperationException(
            "Falta configurar Microservices:UsuariosUrl");

    client.BaseAddress = new Uri(url);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient<EmpleosClient>(client =>
{
    var url = builder.Configuration["Microservices:EmpleosUrl"]
        ?? throw new InvalidOperationException(
            "Falta configurar Microservices:EmpleosUrl");

    client.BaseAddress = new Uri(url);
    client.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "Recomendaciones API v1"
        );

        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.MapRecomendacionesEndpoints();

app.Run();
