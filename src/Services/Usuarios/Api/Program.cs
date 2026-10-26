using Application.Auth;
using Application.PerfilLaboral;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using Usuarios.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
// Solo cambia el formato de los errores automáticos de validación del perfil laboral.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    var defaultFactory = options.InvalidModelStateResponseFactory;
    options.InvalidModelStateResponseFactory = context =>
    {
        if (!context.HttpContext.Request.Path.StartsWithSegments("/api/perfil-laboral"))
            return defaultFactory is not null
                ? defaultFactory(context)
                : new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState));

        var firstError = context.ModelState
            .SelectMany(entry => entry.Value!.Errors.Select(error => new
            {
                Path = entry.Key,
                Text = error.ErrorMessage.Length > 0
                    ? error.ErrorMessage
                    : error.Exception?.Message ?? string.Empty
            }))
            .FirstOrDefault();

        if (firstError is null)
            return new BadRequestObjectResult(new { message = "Revisá los datos enviados de tu perfil laboral." });

        var indexMatch = Regex.Match(firstError.Path, @"\[(\d+)\]");
        var number = indexMatch.Success && int.TryParse(indexMatch.Groups[1].Value, out var index)
            ? index + 1 : 1;
        var lowerPath = firstError.Path.ToLowerInvariant();
        var section = lowerPath.Contains("experiencias") ? $"en la experiencia laboral {number}"
            : lowerPath.Contains("formacion") ? $"en la formación académica {number}"
            : lowerPath.Contains("habilidades") ? $"en la habilidad {number}"
            : lowerPath.Contains("idiomas") ? $"en el idioma {number}" : string.Empty;

        var field = firstError.Path.Split('.').LastOrDefault()?.Trim('$') ?? string.Empty;
        var requiredJson = Regex.Match(firstError.Text,
            @"missing required properties including:\s*'(?<property>[^']+)'",
            RegexOptions.IgnoreCase);
        if (requiredJson.Success)
            field = requiredJson.Groups["property"].Value;

        var normalizedField = field.Trim().ToLowerInvariant();
        var location = section.Length > 0 ? " " + section : string.Empty;
        var missing = requiredJson.Success ||
            firstError.Text.Contains("required", StringComparison.OrdinalIgnoreCase) ||
            firstError.Text.Contains("obligatorio", StringComparison.OrdinalIgnoreCase);
        var tooLong = firstError.Text.Contains("maximum length", StringComparison.OrdinalIgnoreCase) ||
            firstError.Text.Contains("superar", StringComparison.OrdinalIgnoreCase);

        string message;
        if (firstError.Text.Contains("non-empty request body", StringComparison.OrdinalIgnoreCase))
            message = "No recibimos la información de tu perfil laboral. Volvé a intentarlo.";
        else if (normalizedField == "sinexperiencia")
            message = "Indicá si tenés experiencia laboral.";
        else if (normalizedField == "esactual")
            message = $"Indicá si todavía trabajás en la experiencia laboral {number}.";
        else if (normalizedField is "fechainicio" or "fechafin")
        {
            var dateName = normalizedField == "fechainicio" ? "inicio" : "finalización";
            message = missing
                ? $"Ingresá la fecha de {dateName}{location}."
                : $"Revisá la fecha de {dateName}{location}. Ingresá una fecha real con el formato AAAA-MM-DD.";
        }
        else
        {
            var fieldName = normalizedField switch
            {
                "empresa" => "el nombre de la empresa",
                "puesto" => "el puesto",
                "ubicacion" => "la ubicación",
                "descripcion" => "la descripción",
                "institucion" => "el nombre de la institución",
                "titulo" => "el título",
                "niveleducativo" => "el nivel educativo",
                "estado" => "el estado",
                "nombre" => "el nombre de la habilidad",
                "idioma" => "el nombre del idioma",
                "nivel" => "el nivel",
                "resumen" => "el resumen",
                _ => string.Empty
            };
            var max = normalizedField switch
            {
                "empresa" or "institucion" or "titulo" => 150,
                "puesto" or "ubicacion" => 120,
                "nombre" or "idioma" => 100,
                "resumen" or "descripcion" => 2000,
                _ => 0
            };
            message = fieldName.Length == 0
                ? "No pudimos procesar la información de tu perfil laboral. Revisá los datos enviados."
                : missing
                    ? $"Ingresá {fieldName}{location}."
                    : tooLong && max > 0
                        ? $"{char.ToUpperInvariant(fieldName[0]) + fieldName[1..]}{location} no puede superar los {max} caracteres."
                        : $"Revisá {fieldName}{location}. El valor ingresado no es válido.";
        }
        return new BadRequestObjectResult(new { message });
    };
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PerfilLaboralService>();

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

        // Los endpoints de perfil laboral devuelven mensajes comprensibles al usuario.
        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                if (!context.Request.Path.StartsWithSegments("/api/perfil-laboral"))
                    return Task.CompletedTask;

                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return context.Response.WriteAsJsonAsync(new
                {
                    message = "Iniciá sesión para consultar o modificar tu perfil laboral."
                });
            },
            OnForbidden = context =>
            {
                if (!context.Request.Path.StartsWithSegments("/api/perfil-laboral"))
                    return Task.CompletedTask;

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return context.Response.WriteAsJsonAsync(new
                {
                    message = "No tenés permiso para realizar esta operación sobre tu perfil laboral."
                });
            }
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

