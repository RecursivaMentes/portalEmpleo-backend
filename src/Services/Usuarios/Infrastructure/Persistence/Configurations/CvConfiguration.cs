using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Usuarios.Infrastructure.Persistence.Configurations;

public class CvConfiguration : IEntityTypeConfiguration<Cv>
{
    public void Configure(EntityTypeBuilder<Cv> builder)
    {
        builder.ToTable("cvs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Titulo).HasMaxLength(200).IsRequired();
        builder.Property(x => x.UrlArchivo).HasMaxLength(500);
        builder.Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(1L);
        builder.HasIndex(x => x.UsuarioId);
        builder.HasIndex(x => x.UsuarioId, "IX_cvs_usuario_principal")
            .IsUnique().HasFilter("\"EsPrincipal\" = TRUE");

        builder.HasOne(x => x.Usuario)
            .WithMany(x => x.Cvs)
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
