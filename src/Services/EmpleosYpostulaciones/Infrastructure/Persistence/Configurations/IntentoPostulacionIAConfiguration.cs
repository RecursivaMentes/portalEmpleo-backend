using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpleosYpostulaciones.Infrastructure.Persistence.Configurations;

public sealed class IntentoPostulacionIAConfiguration : IEntityTypeConfiguration<IntentoPostulacionIA>
{
    public void Configure(EntityTypeBuilder<IntentoPostulacionIA> builder)
    {
        builder.ToTable("intentos_postulacion_ia");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Resultado).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Motivo).HasMaxLength(500);
        builder.HasIndex(x => new { x.UsuarioId, x.FechaUtc });
        builder.HasIndex(x => new { x.UsuarioId, x.EmpleoId, x.FechaUtc });
    }
}
