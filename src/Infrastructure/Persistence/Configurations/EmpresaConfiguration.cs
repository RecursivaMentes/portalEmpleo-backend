using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class EmpresaConfiguration : IEntityTypeConfiguration<Empresa>
{
    public void Configure(EntityTypeBuilder<Empresa> builder)
    {
        builder.ToTable("empresas");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Rubro).HasMaxLength(100);
        builder.Property(x => x.Ubicacion).HasMaxLength(200);
        builder.Property(x => x.SitioWeb).HasMaxLength(300);

        builder.HasOne(x => x.Usuario)
            .WithMany(x => x.Empresas)
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
