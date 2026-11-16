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

    /// <summary>
    /// Pide un texto al modelo en una sola vuelta: unas instrucciones y una solicitud, sin conversación,
    /// sin herramientas y sin el contexto del chat. Sirve para tareas de redacción como armar un CV.
    /// </summary>
    /// <exception cref="AsistenteNoDisponibleException">Si el modelo no está disponible o no responde.</exception>
    /// <returns>El texto del modelo; cadena vacía si no devolvió nada utilizable (por ejemplo, si se negó).</returns>
    /// <param name="respuestaJson">
    /// Si es true, se le pide al modelo que responda únicamente un objeto JSON (los modelos locales tienen un modo
    /// específico para eso, que evita que mezclen texto con el JSON). Las instrucciones deben pedir igualmente JSON.
    /// </param>
    Task<string> GenerarTextoAsync(
        string instrucciones, string solicitud, CancellationToken cancellationToken, bool respuestaJson = false);
}
