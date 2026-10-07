using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class PerfilLaboralConfiguration : IEntityTypeConfiguration<PerfilLaboral>
{
    public void Configure(EntityTypeBuilder<PerfilLaboral> builder)
    {
        builder.ToTable("perfiles_laborales");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Resumen).HasMaxLength(2000);
        builder.Property(x => x.ExperienciasJson).HasColumnType("jsonb");
        builder.Property(x => x.FormacionJson).HasColumnType("jsonb");
        builder.Property(x => x.HabilidadesJson).HasColumnType("jsonb");
        builder.Property(x => x.IdiomasJson).HasColumnType("jsonb");

        builder.HasOne(x => x.Usuario)
            .WithOne(x => x.PerfilLaboral)
            .HasForeignKey<PerfilLaboral>(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
