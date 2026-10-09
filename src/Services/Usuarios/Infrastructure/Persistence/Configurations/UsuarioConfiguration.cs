using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Usuarios.Infrastructure.Persistence.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => x.Email).IsUnique();
        builder.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Apellido).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Telefono).HasMaxLength(30);
        builder.Property(x => x.PreferenciasJson).HasColumnType("jsonb");
        builder.Property(x => x.ReglasPostulacion).HasMaxLength(2000);
        builder.Property(x => x.Rol).HasConversion<string>().HasMaxLength(30);
    }
}
