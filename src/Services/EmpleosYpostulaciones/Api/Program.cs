using System.Text.Json.Serialization;
using Application.Empleos;
using Application.Postulaciones;
using EmpleosYpostulaciones.Infrastructure.Clients;
using EmpleosYpostulaciones.Infrastructure;
using EmpleosYpostulaciones.Api.Endpoints;
using EmpleosYpostulaciones.Api.Exceptions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<PostulacionesExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<EmpleoService>();
builder.Services.AddScoped<PostulacionService>();
builder.Services.AddHttpClient<ICvValidationClient, CvValidationHttpClient>(client =>
{
    var baseUrl = builder.Configuration["Services:Usuarios:BaseUrl"]
        ?? throw new InvalidOperationException("Falta Services:Usuarios:BaseUrl");
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient<IPerfilLaboralValidationClient, PerfilLaboralValidationHttpClient>(client =>
{
    var baseUrl = builder.Configuration["Services:Usuarios:BaseUrl"]
        ?? throw new InvalidOperationException("Falta Services:Usuarios:BaseUrl");
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter());
});

// Configuración JWT
var issuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Falta Jwt:Issuer");

var audience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Falta Jwt:Audience");

var secret = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Falta Jwt:Key");

var keyBytes = Convert.FromBase64String(secret);

if (keyBytes.Length < 32)
    throw new InvalidOperationException("Clave JWT demasiado corta.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,

                ValidateAudience = true,
                ValidAudience = audience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(keyBytes),

                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,

                NameClaimType = "sub",
                RoleClaimType = "role"
            };
    });

builder.Services.AddAuthorization();

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
}

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapEmpleosEndpoints();
app.MapPostulacionesEndpoints();

app.Run();
