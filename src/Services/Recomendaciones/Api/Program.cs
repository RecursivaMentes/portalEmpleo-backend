using Recomendaciones.Application.Services;
using Recomendaciones.Api.Endpoints;
using Recomendaciones.Api.Exceptions;
using Recomendaciones.Infrastructure.Clients;
using Recomendaciones.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddExceptionHandler<RecomendacionesExceptionHandler>();
builder.Services.AddProblemDetails();

var issuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Falta Jwt:Issuer");
var audience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Falta Jwt:Audience");
var secret = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Falta Jwt:Key");
var keyBytes = Convert.FromBase64String(secret);
if (keyBytes.Length < 32)
    throw new InvalidOperationException("Clave JWT demasiado corta");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization();

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

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
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

app.UseCors("Frontend");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapRecomendacionesEndpoints();

app.Run();
