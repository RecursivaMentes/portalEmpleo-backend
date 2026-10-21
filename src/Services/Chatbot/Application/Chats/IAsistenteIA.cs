using Domain.Enums;

namespace Application.Chats;

public record MensajeIA(RolMensaje Rol, string Contenido);

/// <summary>Se lanza cuando el asistente de IA no está disponible o no puede responder.</summary>
public class AsistenteNoDisponibleException(string message, Exception? inner = null)
    : Exception(message, inner);

public interface IAsistenteIA
{
    /// <summary>
    /// Genera la respuesta del asistente a partir del historial de la conversación.
    /// El último elemento del historial es el mensaje que hay que responder.
    /// </summary>
    Task<string> ResponderAsync(IReadOnlyList<MensajeIA> historial, CancellationToken cancellationToken);
}
