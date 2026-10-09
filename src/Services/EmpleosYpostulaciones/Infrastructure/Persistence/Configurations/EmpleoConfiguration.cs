using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpleosYpostulaciones.Infrastructure.Persistence.Configurations;

public class EmpleoConfiguration : IEntityTypeConfiguration<Empleo>
{
    public void Configure(EntityTypeBuilder<Empleo> builder)
    {
        builder.ToTable("empleos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Titulo).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Ubicacion).HasMaxLength(200);
        builder.Property(x => x.Modalidad).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.TipoContrato).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Estado).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.SalarioMin).HasPrecision(18, 2);
        builder.Property(x => x.SalarioMax).HasPrecision(18, 2);

        builder.Property(x => x.EmpresaId).IsRequired();
        builder.Property(x => x.UsuarioId).IsRequired();

        // Índices optimizados para búsquedas frecuentes
        builder.HasIndex(x => x.Estado);
        builder.HasIndex(x => x.EmpresaId);
        builder.HasIndex(x => x.UsuarioId);
    }
}
