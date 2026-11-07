using Domain.Enums;

namespace Application.Notificaciones.Requests;

public record CrearNotificacionRequest(
    Guid UsuarioId,
    TipoNotificacion Tipo,
    string Mensaje,
    Guid? ConversacionId
);