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
    public sealed record CambioEstado(string Estado, DateTime FechaUtc);
    public IReadOnlyList<CambioEstado> Historial { get; init; } = [];
    public string? TextoPresentacion { get; init; }

    public static PostulacionResponse DesdeEntidad(Postulacion postulacion) => new(
        postulacion.Id,
        postulacion.UsuarioId,
        postulacion.EmpleoId,
        postulacion.CvId,
        postulacion.Estado,
        postulacion.FechaPostulacion)
    {
        TextoPresentacion = postulacion.TextoPresentacion,
        Historial = System.Text.Json.JsonSerializer.Deserialize<List<CambioEstado>>(
            postulacion.HistorialEstadosJson,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) ?? []
    };
}
