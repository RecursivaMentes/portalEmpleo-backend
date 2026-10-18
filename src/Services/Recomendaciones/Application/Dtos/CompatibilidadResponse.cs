namespace Recomendaciones.Application.DTOs;

public class CompatibilidadResponse
{
    public Guid UsuarioId { get; set; }
    public Guid EmpleoId { get; set; }

    public decimal Score { get; set; }

    public List<string> Coincidencias { get; set; } = new();

    public List<string> RequisitosFaltantes { get; set; } = new();

    public string Motivo { get; set; } = string.Empty;
}