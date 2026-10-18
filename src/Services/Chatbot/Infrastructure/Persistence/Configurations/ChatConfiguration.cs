using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chatbot.Infrastructure.Persistence.Configurations;

public class ChatConfiguration : IEntityTypeConfiguration<Chat>
{
    public void Configure(EntityTypeBuilder<Chat> builder)
    {
        builder.ToTable("chats");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(30);

        builder.Property(x => x.UsuarioId)
            .IsRequired();
            
        builder.HasIndex(x => x.UsuarioId); 
    }
}
