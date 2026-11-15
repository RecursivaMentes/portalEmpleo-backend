namespace Domain.Entities;

// Reserva transitoria; bloquea nuevas preparaciones y confirmaciones mientras
// Usuarios persiste una modificación autorizada de CV/perfil/preferencias.
public sealed class ReservaCambioUsuario
{
    public Guid UsuarioId { get; set; }
    public Guid OperacionId { get; set; }
    public DateTime ExpiraUtc { get; set; }
}
