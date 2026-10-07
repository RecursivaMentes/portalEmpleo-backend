using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class MensajeConfiguration : IEntityTypeConfiguration<Mensaje>
{
    public void Configure(EntityTypeBuilder<Mensaje> builder)
    {
        builder.ToTable("mensajes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Rol).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Contenido).IsRequired();

        builder.HasIndex(x => new { x.ChatId, x.Fecha });

        builder.HasOne(x => x.Chat)
            .WithMany(x => x.Mensajes)
            .HasForeignKey(x => x.ChatId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
