using Application.Auth;
using Application.Cvs;
using Application.PerfilLaboral;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Usuarios.Infrastructure.Documentos;
using Usuarios.Infrastructure.Persistence.Repositories;
using Usuarios.Infrastructure.Security;
using Usuarios.Infrastructure.Storage;

namespace Usuarios.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Falta la cadena de conexión 'DefaultConnection'.");

        services.AddDbContext<AppDbContext>(
            options => options.UseNpgsql(connectionString));

        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<Application.Cvs.ICvRepository, CvRepository>();
        services.AddScoped<IPerfilLaboralRepository, PerfilLaboralRepository>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ITokenService, JwtTokenService>();

        services.AddSingleton<IExtractorTextoCv, ExtractorTextoCv>();

        // Generador del documento descargable del CV (hoy solo PDF).
        services.AddSingleton<IGeneradorDocumentoCv, GeneradorPdfCv>();

        // Los archivos de CV se guardan en una carpeta. "Almacenamiento:RutaCvs" puede ser absoluta
        // (recomendado en producción) o relativa a la carpeta del proyecto (por defecto App_Data/cvs).
        var rutaCvs = configuration["Almacenamiento:RutaCvs"];
        services.AddSingleton<IAlmacenamientoArchivos>(sp =>
        {
            var raiz = string.IsNullOrWhiteSpace(rutaCvs) ? "App_Data/cvs" : rutaCvs;
            if (!Path.IsPathRooted(raiz))
                raiz = Path.Combine(sp.GetRequiredService<IHostEnvironment>().ContentRootPath, raiz);

            return new AlmacenamientoArchivosLocal(raiz, sp.GetRequiredService<ILogger<AlmacenamientoArchivosLocal>>());
        });

        return services;
    }
}
