using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Usuarios.Infrastructure.Persistence.Configurations;

public class NotificacionConfiguration : IEntityTypeConfiguration<Notificacion>
{
    public void Configure(EntityTypeBuilder<Notificacion> builder)
    {
        builder.ToTable("notificaciones");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Mensaje).HasMaxLength(1000).IsRequired();

        builder.Property(x => x.ConversacionId).IsRequired();
        
        builder.HasIndex(x => new { x.ConversacionId, x.Leido });

    }
}
