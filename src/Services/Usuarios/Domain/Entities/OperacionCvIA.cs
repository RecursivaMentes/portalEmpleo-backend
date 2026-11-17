namespace Domain.Entities;

// Solo registra cambios de CV expresamente aprobados. El Id es el de la preparacion.
public sealed class OperacionCvIA
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Guid CvId { get; set; }
    public long VersionResultado { get; set; }
    public string HuellaSolicitud { get; set; } = string.Empty;
    public DateTime FechaUtc { get; set; } = DateTime.UtcNow;
}