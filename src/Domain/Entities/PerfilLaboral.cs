namespace Domain.Entities;

public class PerfilLaboral
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public string? Resumen { get; set; }
    public string ExperienciasJson { get; set; } = "[]";
    public string FormacionJson { get; set; } = "[]";
    public string HabilidadesJson { get; set; } = "[]";
    public string IdiomasJson { get; set; } = "[]";
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;

    public Usuario Usuario { get; set; } = null!;
}
