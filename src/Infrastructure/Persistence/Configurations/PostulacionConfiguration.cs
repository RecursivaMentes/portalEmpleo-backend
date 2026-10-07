using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class PostulacionConfiguration : IEntityTypeConfiguration<Postulacion>
{
    public void Configure(EntityTypeBuilder<Postulacion> builder)
    {
        builder.ToTable("postulaciones");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Estado).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Observaciones).HasMaxLength(2000);
        builder.Property(x => x.TextoPresentacion).HasMaxLength(4000);
        builder.Property(x => x.HistorialEstadosJson).HasColumnType("jsonb");

        // Un usuario solo puede postularse una vez a cada empleo.
        builder.HasIndex(x => new { x.UsuarioId, x.EmpleoId }).IsUnique();

        builder.HasOne(x => x.Usuario)
            .WithMany(x => x.Postulaciones)
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Empleo)
            .WithMany(x => x.Postulaciones)
            .HasForeignKey(x => x.EmpleoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Cv)
            .WithMany(x => x.Postulaciones)
            .HasForeignKey(x => x.CvId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
