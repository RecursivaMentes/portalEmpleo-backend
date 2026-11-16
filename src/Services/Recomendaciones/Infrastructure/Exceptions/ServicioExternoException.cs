namespace Recomendaciones.Infrastructure.Exceptions;

public class ServicioExternoException : Exception
{
    public string Servicio { get; }

    public ServicioExternoException(
        string servicio,
        string mensaje,
        Exception? innerException = null)
        : base(mensaje, innerException)
    {
        Servicio = servicio;
    }
}
