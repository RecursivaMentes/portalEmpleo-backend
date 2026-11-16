namespace Application.Cvs.Responses;

/// <summary>Vista corta de un CV para listados. No incluye el contenido.</summary>
public record CvResumenResponse(
    Guid Id,
    string Titulo,
    bool EsPrincipal,
    bool TieneArchivo,
    DateTime FechaCreacion,
    DateTime FechaActualizacion,
    long Version);
