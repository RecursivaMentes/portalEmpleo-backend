namespace Recomendaciones.Application.Integrations;

public class EmpleoDto
{
    public Guid Id { get; set; }

    public string Titulo { get; set; } = string.Empty;

    public string? Requisitos { get; set; }

    public string? Ubicacion { get; set; }

    public int Modalidad { get; set; }

    public int Estado { get; set; }

    public DateTime? FechaCierre { get; set; }
}
