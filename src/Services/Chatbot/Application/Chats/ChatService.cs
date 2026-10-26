using Application.Herramientas;
using Application.Perfil;
using Domain.Entities;
using Domain.Enums;

namespace Application.Chats;

public class ChatService(
    IChatRepository chats, IAsistenteIA asistente, IPerfilUsuarioClient perfiles, OpcionesChat opciones)
{
    /// <summary>Cantidad máxima de mensajes previos que se envían al modelo como contexto.</summary>
    private const int LimiteContexto = 50;

    public async Task<ChatResponse> CrearChatAsync(
        Guid usuarioId, CrearChatRequest request, CancellationToken cancellationToken)
    {
        var chat = new Chat
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Tipo = request.Tipo,
        };

        await chats.AddChatAsync(chat, cancellationToken);

        return new ChatResponse(chat.Id, chat.Tipo, chat.FechaInicio);
    }

    public async Task<IReadOnlyList<ChatResponse>> ListarChatsAsync(
        Guid usuarioId, CancellationToken cancellationToken)
    {
        var lista = await chats.ListarChatsAsync(usuarioId, cancellationToken);

        return lista.Select(c => new ChatResponse(c.Id, c.Tipo, c.FechaInicio)).ToList();
    }

    /// <summary>Devuelve el historial del chat, o null si no existe o no pertenece al usuario.</summary>
    public async Task<IReadOnlyList<MensajeResponse>?> ObtenerHistorialAsync(
        Guid usuarioId, Guid chatId, CancellationToken cancellationToken)
    {
        var chat = await chats.ObtenerChatAsync(chatId, usuarioId, cancellationToken);
        if (chat is null) return null;

        var mensajes = await chats.ObtenerMensajesAsync(chatId, int.MaxValue, cancellationToken);

        return mensajes.Select(ToResponse).ToList();
    }

    /// <summary>
    /// Envía un mensaje del usuario, obtiene la respuesta del asistente y guarda ambos.
    /// Devuelve null si el chat no existe o no pertenece al usuario.
    /// </summary>
    /// <param name="accessToken">Token del usuario, que se reenvía a Usuarios para consultar su perfil.</param>
    /// <exception cref="AsistenteNoDisponibleException">
    /// Si el asistente falla no se guarda nada, así el historial no queda con mensajes sin respuesta.
    /// </exception>
    public async Task<EnviarMensajeResponse?> EnviarMensajeAsync(
        Guid usuarioId, string accessToken, Guid chatId, EnviarMensajeRequest request,
        CancellationToken cancellationToken)
    {
        var chat = await chats.ObtenerChatAsync(chatId, usuarioId, cancellationToken);
        if (chat is null) return null;

        var previos = await chats.ObtenerMensajesAsync(chatId, LimiteContexto, cancellationToken);

        var mensajeUsuario = new Mensaje
        {
            Id = Guid.NewGuid(),
            ChatId = chatId,
            Rol = RolMensaje.Usuario,
            Contenido = request.Contenido.Trim(),
        };

        var historial = previos
            .Append(mensajeUsuario)
            .Select(m => new MensajeIA(m.Rol, m.Contenido))
            .ToList();

        // El perfil se relee en cada mensaje: así el asistente ve siempre la versión vigente.
        var perfil = await perfiles.ObtenerAsync(accessToken, cancellationToken);
        var contextoPerfil = perfil is null ? null : ContextoPerfil.Construir(perfil);

        // Solo se ofrecen herramientas si se pudo leer el perfil: sin él no se podría guardar nada con criterio.
        IEjecutorHerramientas? herramientas = opciones.HerramientasPerfil && perfil is not null
            ? new HerramientasPerfil(perfiles, accessToken)
            : null;

        var textoRespuesta = await asistente.ResponderAsync(historial, contextoPerfil, herramientas, cancellationToken);

        var respuesta = new Mensaje
        {
            Id = Guid.NewGuid(),
            ChatId = chatId,
            Rol = RolMensaje.Asistente,
            Contenido = textoRespuesta,
            // Un instante después, para que el orden por fecha sea siempre usuario -> asistente.
            Fecha = mensajeUsuario.Fecha.AddMilliseconds(1),
        };

        await chats.AddMensajesAsync([mensajeUsuario, respuesta], cancellationToken);

        return new EnviarMensajeResponse(ToResponse(mensajeUsuario), ToResponse(respuesta));
    }

    private static MensajeResponse ToResponse(Mensaje m) => new(m.Id, m.Rol, m.Contenido, m.Fecha);
}
