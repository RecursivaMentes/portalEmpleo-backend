namespace Domain.Entities;

public class Empresa
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? Rubro { get; set; }
    public string? Ubicacion { get; set; }
    public string? SitioWeb { get; set; }
    public Guid UsuarioId { get; set; }
    public DateTime FechaAlta { get; set; } = DateTime.UtcNow;

    public Usuario Usuario { get; set; } = null!;
    public ICollection<Empleo> Empleos { get; set; } = [];
}
