using Application.Chats;
using Application.Cvs;
using Chatbot.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Los enums viajan como texto ("Asistente") y no como número, tanto al recibir como al responder.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Configuración nativa de .NET 10 para documentación de APIs
builder.Services.AddOpenApi();

// Solo para la importación de datos desde un CV: si el pedido no tiene la forma esperada (por ejemplo, una
// experiencia sin empresa), se responde { message } en español, como el resto de los errores del servicio,
// en vez del ProblemDetails en inglés del framework. El detalle técnico va en "errores".
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    var porDefecto = options.InvalidModelStateResponseFactory;
    options.InvalidModelStateResponseFactory = contexto =>
    {
        if (!contexto.HttpContext.Request.Path.StartsWithSegments("/api/analisis-cv"))
            return porDefecto(contexto);

        return new BadRequestObjectResult(new
        {
            message = "Los datos enviados no son válidos. Revisá que cada experiencia tenga empresa y puesto, " +
                      "cada formación su institución y cada habilidad e idioma su nombre.",
            errores = contexto.ModelState
                .Where(e => e.Value is { Errors.Count: > 0 })
                .ToDictionary(e => e.Key, e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray()),
        });
    };
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<GeneracionCvService>();
builder.Services.AddScoped<AnalisisCvService>();
builder.Services.AddScoped<AplicacionPropuestaService>();
builder.Services.AddSingleton<GeneracionesEnCurso>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// El token lo emite el servicio Usuarios; acá solo se valida con el mismo Issuer, Audience y clave.
var issuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Falta Jwt:Issuer");

var audience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Falta Jwt:Audience");

var secret = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Falta Jwt:Key");

var keyBytes = Convert.FromBase64String(secret);

if (keyBytes.Length < 32)
    throw new InvalidOperationException("Clave JWT demasiado corta");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
