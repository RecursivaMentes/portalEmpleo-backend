using Application.Chats;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Infrastructure.Persistence.Repositories;

public class ChatRepository(AppDbContext db) : IChatRepository
{
    public async Task AddChatAsync(Chat chat, CancellationToken cancellationToken)
    {
        db.Chats.Add(chat);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Chat>> ListarChatsAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        return await db.Chats
            .AsNoTracking()
            .Where(c => c.UsuarioId == usuarioId)
            .OrderByDescending(c => c.FechaInicio)
            .ToListAsync(cancellationToken);
    }

    public Task<Chat?> ObtenerChatAsync(Guid chatId, Guid usuarioId, CancellationToken cancellationToken)
    {
        return db.Chats
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == chatId && c.UsuarioId == usuarioId, cancellationToken);
    }

    public async Task<IReadOnlyList<Mensaje>> ObtenerMensajesAsync(
        Guid chatId, int limite, CancellationToken cancellationToken)
    {
        // Se piden los más recientes y se devuelven en orden cronológico.
        var recientes = await db.Mensajes
            .AsNoTracking()
            .Where(m => m.ChatId == chatId)
            .OrderByDescending(m => m.Fecha)
            .Take(limite)
            .ToListAsync(cancellationToken);

        recientes.Reverse();
        return recientes;
    }

    public async Task AddMensajesAsync(IEnumerable<Mensaje> mensajes, CancellationToken cancellationToken)
    {
        db.Mensajes.AddRange(mensajes);
        await db.SaveChangesAsync(cancellationToken);
    }
}
