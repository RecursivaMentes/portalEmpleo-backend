using Application.Herramientas;
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
    /// <param name="contextoPerfil">
    /// Texto con los datos actuales del perfil de la persona, o null si no se pudo obtener.
    /// </param>
    /// <param name="herramientas">
    /// Herramientas que el modelo puede llamar durante la respuesta, o null si no se le ofrece ninguna.
    /// El asistente se encarga de repetir el ciclo "el modelo pide una herramienta, se ejecuta, se le
    /// devuelve el resultado" hasta que responda con texto, con un tope de <see cref="LimitesHerramientas.MaxRondas"/>.
    /// </param>
    Task<string> ResponderAsync(
        IReadOnlyList<MensajeIA> historial,
        string? contextoPerfil,
        IEjecutorHerramientas? herramientas,
        CancellationToken cancellationToken);
}
