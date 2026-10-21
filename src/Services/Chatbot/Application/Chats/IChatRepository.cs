using Domain.Entities;

namespace Application.Chats;

public interface IChatRepository
{
    Task AddChatAsync(Chat chat, CancellationToken cancellationToken);

    Task<IReadOnlyList<Chat>> ListarChatsAsync(Guid usuarioId, CancellationToken cancellationToken);

    /// <summary>Devuelve el chat solo si pertenece al usuario indicado.</summary>
    Task<Chat?> ObtenerChatAsync(Guid chatId, Guid usuarioId, CancellationToken cancellationToken);

    /// <summary>Devuelve los últimos <paramref name="limite"/> mensajes del chat, en orden cronológico.</summary>
    Task<IReadOnlyList<Mensaje>> ObtenerMensajesAsync(Guid chatId, int limite, CancellationToken cancellationToken);

    Task AddMensajesAsync(IEnumerable<Mensaje> mensajes, CancellationToken cancellationToken);
}
