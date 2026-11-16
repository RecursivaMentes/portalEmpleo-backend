namespace Application.Notificaciones.Responses;

public record NotificacionResponse(
    Guid Id,
    Guid? ConversacionId,
    string Tipo,
    string Mensaje,
    bool Leido,
    DateTime Fecha
);