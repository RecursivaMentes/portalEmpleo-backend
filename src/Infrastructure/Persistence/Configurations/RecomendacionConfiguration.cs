using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class RecomendacionConfiguration : IEntityTypeConfiguration<Recomendacion>
{
    public void Configure(EntityTypeBuilder<Recomendacion> builder)
    {
        builder.ToTable("recomendaciones");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Score).HasPrecision(5, 4);
        builder.Property(x => x.Motivo).HasMaxLength(1000);

        builder.HasIndex(x => new { x.UsuarioId, x.EmpleoId }).IsUnique();

        builder.HasOne(x => x.Usuario)
            .WithMany(x => x.Recomendaciones)
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Empleo)
            .WithMany(x => x.Recomendaciones)
            .HasForeignKey(x => x.EmpleoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
