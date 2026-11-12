using Domain.Entities;
using Domain.Enums;

namespace Application.Postulaciones;

public sealed record PostulacionResponse(
    Guid Id,
    Guid UsuarioId,
    Guid EmpleoId,
    Guid CvId,
    EstadoPostulacion Estado,
    DateTime FechaPostulacion)
{
    public static PostulacionResponse DesdeEntidad(Postulacion postulacion) => new(
        postulacion.Id,
        postulacion.UsuarioId,
        postulacion.EmpleoId,
        postulacion.CvId,
        postulacion.Estado,
        postulacion.FechaPostulacion);
}
