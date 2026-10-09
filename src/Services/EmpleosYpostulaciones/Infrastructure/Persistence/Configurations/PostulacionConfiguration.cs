using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpleosYpostulaciones.Infrastructure.Persistence.Configurations;

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

        builder.Property(x => x.UsuarioId).IsRequired();
        builder.Property(x => x.CvId).IsRequired();

        // Un usuario solo puede postularse una vez a cada empleo.
        builder.HasIndex(x => new { x.UsuarioId, x.EmpleoId }).IsUnique();

        builder.HasIndex(x => x.CvId);

        builder.HasOne(x => x.Empleo)
            .WithMany(x => x.Postulaciones)
            .HasForeignKey(x => x.EmpleoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
