namespace Recomendaciones.Application.Integrations;

public class PerfilLaboralDto
{
    public List<Dictionary<string, string?>> Habilidades { get; set; } = new();

    public string? Resumen { get; set; }
}
