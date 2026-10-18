using Microsoft.EntityFrameworkCore;
using Domain.Entities;

namespace Usuarios.Infrastructure;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<Empresa> Empresas { get; set; }
    public DbSet<PerfilLaboral> PerfilesLaborales { get; set; }
    public DbSet<Cv> Cvs { get; set; }
    public DbSet<Notificacion> Notificaciones { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
