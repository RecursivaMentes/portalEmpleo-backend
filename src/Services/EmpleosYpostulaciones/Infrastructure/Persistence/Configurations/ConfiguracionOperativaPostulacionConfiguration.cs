using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpleosYpostulaciones.Infrastructure.Persistence.Configurations;

public sealed class ConfiguracionOperativaPostulacionConfiguration : IEntityTypeConfiguration<ConfiguracionOperativaPostulacion>
{
    public void Configure(EntityTypeBuilder<ConfiguracionOperativaPostulacion> b)
    {
        b.ToTable("configuraciones_postulacion");
        b.HasKey(x => x.UsuarioId);
        b.Property(x => x.Modo).IsRequired().HasMaxLength(25);
        b.Property(x => x.OrdenPendientes).IsRequired().HasMaxLength(30);
        b.Property(x => x.ZonaHoraria).IsRequired().HasMaxLength(100);
        b.Property(x => x.Version).IsConcurrencyToken();
    }
}
