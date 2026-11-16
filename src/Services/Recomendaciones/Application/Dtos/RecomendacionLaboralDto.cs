namespace Recomendaciones.Application.DTOs;

public class RecomendacionLaboralDto
{
    public Guid EmpleoId { get; set; }

    public string Titulo { get; set; } = string.Empty;

    public decimal PorcentajeCompatibilidad { get; set; }

    public List<string> Coincidencias { get; set; } = new();

    public List<string> RequisitosFaltantes { get; set; } = new();

    public string Motivo { get; set; } = string.Empty;
}
