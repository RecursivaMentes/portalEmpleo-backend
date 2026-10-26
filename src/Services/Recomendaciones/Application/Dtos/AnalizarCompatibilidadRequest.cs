namespace Recomendaciones.Application.DTOs;

public class AnalizarCompatibilidadRequest
{
    public Guid UsuarioId { get; set; }
    public Guid EmpleoId { get; set; }

    public List<string> Habilidades { get; set; } = new();

    public string Requisitos { get; set; } = string.Empty;
}