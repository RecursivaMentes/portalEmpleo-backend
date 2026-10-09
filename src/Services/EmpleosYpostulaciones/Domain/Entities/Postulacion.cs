using Domain.Enums;

namespace Domain.Entities;

public class Postulacion
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Guid EmpleoId { get; set; }
    public Guid? CvId { get; set; }
    public EstadoPostulacion Estado { get; set; } = EstadoPostulacion.Pendiente;
    public string? Observaciones { get; set; }
    public string? TextoPresentacion { get; set; }
    public bool RequiereAprobacion { get; set; }
    public string HistorialEstadosJson { get; set; } = "[]";
    public DateTime FechaPostulacion { get; set; } = DateTime.UtcNow;

    public Empleo Empleo { get; set; } = null!;
}
