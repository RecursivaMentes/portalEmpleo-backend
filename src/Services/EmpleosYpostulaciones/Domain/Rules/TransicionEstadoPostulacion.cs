using Domain.Enums;

namespace Domain.Rules;

public static class TransicionEstadoPostulacion
{
    public static bool Permitida(EstadoPostulacion actual, EstadoPostulacion nuevo, bool esPostulante)
    {
        if (actual == nuevo) return true;
        if (esPostulante)
            return nuevo == EstadoPostulacion.Retirada && actual is
                EstadoPostulacion.Pendiente or EstadoPostulacion.EnRevision or EstadoPostulacion.Entrevista;
        return (actual, nuevo) switch
        {
            (EstadoPostulacion.Pendiente, EstadoPostulacion.EnRevision) => true,
            (EstadoPostulacion.Pendiente, EstadoPostulacion.Rechazada) => true,
            (EstadoPostulacion.EnRevision, EstadoPostulacion.Entrevista) => true,
            (EstadoPostulacion.EnRevision, EstadoPostulacion.Aceptada) => true,
            (EstadoPostulacion.EnRevision, EstadoPostulacion.Rechazada) => true,
            (EstadoPostulacion.Entrevista, EstadoPostulacion.Aceptada) => true,
            (EstadoPostulacion.Entrevista, EstadoPostulacion.Rechazada) => true,
            _ => false
        };
    }
}