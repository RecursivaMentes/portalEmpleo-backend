using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpleosYpostulaciones.Infrastructure.Persistence.Configurations;

public sealed class EmpleoPendienteConfiguration : IEntityTypeConfiguration<EmpleoPendiente>
{
    public void Configure(EntityTypeBuilder<EmpleoPendiente> builder)
    {
        builder.ToTable("empleos_pendientes");
        builder.HasKey(x => new { x.UsuarioId, x.EmpleoId });
        builder.Property(x => x.FechaAgregado).IsRequired();
        builder.HasOne<Empleo>().WithMany().HasForeignKey(x => x.EmpleoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
