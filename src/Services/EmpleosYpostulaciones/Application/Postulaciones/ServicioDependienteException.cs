namespace Application.Postulaciones;

public sealed class ServicioDependienteException(string servicio, string detalle, Exception? causa = null)
    : Exception(detalle, causa)
{
    public string Servicio { get; } = servicio;
    public string CodigoError => "SERVICIO_" + Servicio.ToUpperInvariant() + "_NO_DISPONIBLE";
}