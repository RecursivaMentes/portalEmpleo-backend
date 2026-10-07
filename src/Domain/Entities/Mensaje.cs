using Domain.Enums;

namespace Domain.Entities;

public class Mensaje
{
    public Guid Id { get; set; }
    public Guid ChatId { get; set; }
    public RolMensaje Rol { get; set; }
    public string Contenido { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    public Chat Chat { get; set; } = null!;
}
