using Domain.Enums;

namespace Domain.Entities;

public class Usuario
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string PreferenciasJson { get; set; } = "{}";
    public string? ReglasPostulacion { get; set; }
    public RolUsuario Rol { get; set; } = RolUsuario.Postulante;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public bool Estado { get; set; } = true;

    public PerfilLaboral? PerfilLaboral { get; set; }
    public ICollection<Cv> Cvs { get; set; } = [];
    public ICollection<Empresa> Empresas { get; set; } = [];
}
