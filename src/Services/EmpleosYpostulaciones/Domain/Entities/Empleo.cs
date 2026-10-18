using Domain.Enums; 

namespace Domain.Entities; 

public class Empleo
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid UsuarioId { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? Requisitos { get; set; }
    public string? Ubicacion { get; set; }
    public ModalidadTrabajo Modalidad { get; set; }
    public TipoContrato TipoContrato { get; set; }
    public decimal? SalarioMin { get; set; }
    public decimal? SalarioMax { get; set; }
    public EstadoEmpleo Estado { get; set; } = EstadoEmpleo.Borrador;
    public DateTime FechaPublicacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaCierre { get; set; }

    public ICollection<Postulacion> Postulaciones { get; set; } = [];
  
}
