namespace Application.Cvs.Responses;

/// <summary>
/// Detalle de un CV. <see cref="TieneArchivo"/> indica si hay un archivo asociado; la ubicación
/// interna del archivo no se expone.
/// </summary>
public record CvResponse(
    Guid Id,
    string Titulo,
    string? TextoPlano,
    bool EsPrincipal,
    bool TieneArchivo,
    DateTime FechaCreacion,
    DateTime FechaActualizacion);
