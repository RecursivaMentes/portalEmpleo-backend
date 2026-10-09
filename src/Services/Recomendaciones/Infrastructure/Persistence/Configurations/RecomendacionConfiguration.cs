using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Recomendaciones.Infrastructure.Persistence.Configurations;

public class RecomendacionConfiguration : IEntityTypeConfiguration<Recomendacion>
{
    public void Configure(EntityTypeBuilder<Recomendacion> builder)
    {
        builder.ToTable("recomendaciones");
        builder.HasKey(x => x.Id);

        // Propiedades básicas del algoritmo
        builder.Property(x => x.Score).HasPrecision(5, 4);
        builder.Property(x => x.Motivo).HasMaxLength(1000);

        // Claves de entidades externas convertidas a datos planos obligatorios
        builder.Property(x => x.UsuarioId).IsRequired();
        builder.Property(x => x.EmpleoId).IsRequired();

        //Asegura un único score por combinación de usuario y empleo
        builder.HasIndex(x => new { x.UsuarioId, x.EmpleoId }).IsUnique();
        
        // Índices individuales para búsquedas rápidas 
        builder.HasIndex(x => x.EmpleoId);
    }
}

