using Domain.Enums;

namespace Domain.Entities;

public class Notificacion
{
    public Guid Id { get; set; }
    public Guid ConversacionId { get; set; }
    public TipoNotificacion Tipo { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public bool Leido { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}
