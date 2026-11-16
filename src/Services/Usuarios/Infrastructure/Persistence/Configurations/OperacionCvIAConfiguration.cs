using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Usuarios.Infrastructure.Persistence.Configurations;

public sealed class OperacionCvIAConfiguration : IEntityTypeConfiguration<OperacionCvIA>
{
    public void Configure(EntityTypeBuilder<OperacionCvIA> b)
    {
        b.ToTable("operaciones_cv_ia");
        b.HasKey(x => x.Id);
        b.Property(x => x.HuellaSolicitud).HasMaxLength(64).IsRequired();
        b.Property(x => x.FechaUtc).IsRequired();
        b.HasIndex(x => x.UsuarioId);
    }
}