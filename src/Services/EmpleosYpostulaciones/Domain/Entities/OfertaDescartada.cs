namespace Domain.Entities;

// Exclusión explícita del procesamiento autónomo, independiente de la lista de pendientes.
public sealed class OfertaDescartada
{
    public Guid UsuarioId { get; set; }
    public Guid EmpleoId { get; set; }
    public DateTime FechaUtc { get; set; } = DateTime.UtcNow;
}
