namespace Domain.Entities;

public class Cv
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? UrlArchivo { get; set; }
    public string? TextoPlano { get; set; }
    public bool EsPrincipal { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;

    public Usuario Usuario { get; set; } = null!;
}
