namespace Domain.Entities;

// Referencia minima a una oferta guardada, no es una postulacion.
public class EmpleoPendiente
{
    public Guid UsuarioId { get; set; }
    public Guid EmpleoId { get; set; }
    public DateTime FechaAgregado { get; set; } = DateTime.UtcNow;
}
