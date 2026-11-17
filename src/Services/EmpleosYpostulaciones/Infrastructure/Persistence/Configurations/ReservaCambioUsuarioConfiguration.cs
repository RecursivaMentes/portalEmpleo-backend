using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpleosYpostulaciones.Infrastructure.Persistence.Configurations;

public sealed class ReservaCambioUsuarioConfiguration : IEntityTypeConfiguration<ReservaCambioUsuario>
{
    public void Configure(EntityTypeBuilder<ReservaCambioUsuario> b)
    {
        b.ToTable("reservas_cambio_usuario");
        b.HasKey(x => x.UsuarioId);
        b.HasIndex(x => x.ExpiraUtc);
    }
}
