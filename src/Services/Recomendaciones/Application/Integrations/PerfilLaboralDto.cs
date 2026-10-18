namespace Recomendaciones.Application.Integrations;

public class PerfilLaboralDto
{
    public Guid UsuarioId { get; set; }

    public string HabilidadesJson { get; set; } = "[]";

    public string? Resumen { get; set; }
}
