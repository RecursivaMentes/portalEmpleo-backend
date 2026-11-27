using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpleosYpostulaciones.Infrastructure.Persistence.Configurations;

public sealed class OfertaDescartadaConfiguration : IEntityTypeConfiguration<OfertaDescartada>
{
    public void Configure(EntityTypeBuilder<OfertaDescartada> builder)
    {
        builder.ToTable("ofertas_descartadas");
        builder.HasKey(x => new { x.UsuarioId, x.EmpleoId });
        builder.Property(x => x.FechaUtc).IsRequired();
        builder.HasOne<Empleo>().WithMany().HasForeignKey(x => x.EmpleoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
