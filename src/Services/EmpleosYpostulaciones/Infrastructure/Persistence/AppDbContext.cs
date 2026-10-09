using Microsoft.EntityFrameworkCore;
using Domain.Entities;

namespace EmpleosYpostulaciones.Infrastructure;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Tablas exclusivas de este microservicio
    public DbSet<Empleo> Empleos { get; set; }
    public DbSet<Postulacion> Postulaciones { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
