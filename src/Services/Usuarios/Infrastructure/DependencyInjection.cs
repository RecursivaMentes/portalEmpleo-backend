
using Application.Auth;
using Application.Notificaciones;
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
using Usuarios.Infrastructure.Workers;
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
        services.AddScoped<ICvRepository, CvRepository>();
        services.AddScoped<IPerfilLaboralRepository, PerfilLaboralRepository>();
        services.AddScoped<INotificacionRepository, NotificacionRepository>();

        services.AddSingleton<INotificacionQueue, NotificacionQueue>();
        services.AddHostedService<NotificacionWorker>();

        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ITokenService, JwtTokenService>();

        services.AddSingleton<IExtractorTextoCv, ExtractorTextoCv>();
        services.AddSingleton<IGeneradorDocumentoCv, GeneradorPdfCv>();

        var rutaCvs = configuration["Almacenamiento:RutaCvs"];
        services.AddSingleton<IAlmacenamientoArchivos>(sp =>
        {
            var raiz = string.IsNullOrWhiteSpace(rutaCvs)
                ? "App_Data/cvs"
                : rutaCvs;

            if (!Path.IsPathRooted(raiz))
            {
                raiz = Path.Combine(
                    sp.GetRequiredService<IHostEnvironment>().ContentRootPath,
                    raiz);
            }

            return new AlmacenamientoArchivosLocal(
                raiz,
                sp.GetRequiredService<ILogger<AlmacenamientoArchivosLocal>>());
        });

        return services;
    }
}