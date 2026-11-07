using Application.Auth;
using Application.Notificaciones;
using Application.PerfilLaboral;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Usuarios.Infrastructure.Persistence.Repositories;
using Usuarios.Infrastructure.Security;
using Usuarios.Infrastructure.Workers;

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
        services.AddScoped<IPerfilLaboralRepository, PerfilLaboralRepository>();
        services.AddScoped<INotificacionRepository, NotificacionRepository>();

        services.AddSingleton<INotificacionQueue, NotificacionQueue>();
        services.AddHostedService<NotificacionWorker>();

        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ITokenService, JwtTokenService>();

        return services;
    }
}