using Domain.Enums;

namespace Domain.Entities;

// Trabajo previo al envio, iniciado por el usuario o por la IA.
// La postulacion real se crea exclusivamente al confirmar el envio.
public class PreparacionPostulacion
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Guid EmpleoId { get; set; }
    public Guid? CvId { get; set; }
    public string? TextoPresentacion { get; set; }
    public OrigenPreparacionPostulacion Origen { get; set; }
    public EstadoPreparacionPostulacion Estado { get; set; } = EstadoPreparacionPostulacion.Preparando;
    public Guid? PostulacionId { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;
}
