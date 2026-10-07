using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Empleo> Empleos => Set<Empleo>();
    public DbSet<Cv> Cvs => Set<Cv>();
    public DbSet<Postulacion> Postulaciones => Set<Postulacion>();
    public DbSet<PerfilLaboral> PerfilesLaborales => Set<PerfilLaboral>();
    public DbSet<Recomendacion> Recomendaciones => Set<Recomendacion>();
    public DbSet<Chat> Chats => Set<Chat>();
    public DbSet<Mensaje> Mensajes => Set<Mensaje>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
