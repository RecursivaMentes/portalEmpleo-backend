namespace Application.Postulaciones;

public record CrearPostulacionRequest(
    Guid UsuarioId,
    Guid EmpleoId,
    Guid CvId,
    string? TextoPresentacion
);
