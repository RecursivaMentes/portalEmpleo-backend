namespace Domain.Entities;

public class Recomendacion
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Guid EmpleoId { get; set; }
    public decimal Score { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string? Motivo { get; set; }

    public Usuario Usuario { get; set; } = null!;
    public Empleo Empleo { get; set; } = null!;
}
