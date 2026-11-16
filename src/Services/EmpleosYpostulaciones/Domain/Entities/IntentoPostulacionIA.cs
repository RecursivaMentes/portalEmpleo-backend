namespace Domain.Entities;

public sealed class IntentoPostulacionIA
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Guid EmpleoId { get; set; }
    public Guid? PreparacionId { get; set; }
    public string Resultado { get; set; } = string.Empty;
    public string? Motivo { get; set; }
    public DateTime FechaUtc { get; set; } = DateTime.UtcNow;
}
