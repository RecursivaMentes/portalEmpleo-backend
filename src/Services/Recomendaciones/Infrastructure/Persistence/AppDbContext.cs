using Microsoft.EntityFrameworkCore;
using Domain.Entities;

namespace Recomendaciones.Infrastructure;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Tablas exclusivas de este microservicio
    public DbSet<Recomendacion> Recomendaciones { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
