namespace Domain.Entities;

// Parametros operativos del envio. Las preferencias laborales siguen en Usuarios.
public sealed class ConfiguracionOperativaPostulacion
{
    public Guid UsuarioId { get; set; }
    public string Modo { get; set; } = "Desactivada";
    public int CompatibilidadMinima { get; set; } = 60;
    public int LimiteDiario { get; set; } = 5;
    public string OrdenPendientes { get; set; } = "Prioridad";
    public string ZonaHoraria { get; set; } = "America/Argentina/Buenos_Aires";
    public int Version { get; set; }
}
