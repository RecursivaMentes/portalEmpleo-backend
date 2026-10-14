using Domain.Enums;

namespace Domain.Entities;

public class Chat
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public TipoChat Tipo { get; set; }
    public DateTime FechaInicio { get; set; } = DateTime.UtcNow;

    public ICollection<Mensaje> Mensajes { get; set; } = [];
}
