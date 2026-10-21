using Domain.Enums;

namespace Application.Chats;

public record CrearChatRequest(TipoChat Tipo = TipoChat.Asistente);

public record EnviarMensajeRequest(string Contenido);

public record ChatResponse(Guid Id, TipoChat Tipo, DateTime FechaInicio);

public record MensajeResponse(Guid Id, RolMensaje Rol, string Contenido, DateTime Fecha);

public record EnviarMensajeResponse(MensajeResponse MensajeUsuario, MensajeResponse Respuesta);
