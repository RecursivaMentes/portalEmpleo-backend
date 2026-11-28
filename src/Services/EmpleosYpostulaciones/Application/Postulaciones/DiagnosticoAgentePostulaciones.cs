using System.Collections.Concurrent;

namespace Application.Postulaciones;

public sealed record EvaluacionAgente(string Estado, string Mensaje, DateTime FechaUtc, int? Candidatos = null);

// Estado de la última evaluación de cada cuenta en este proceso. No es historial persistente.
public sealed class DiagnosticoAgentePostulaciones
{
    private readonly ConcurrentDictionary<Guid, EvaluacionAgente> evaluaciones = new();
    public EvaluacionAgente? Obtener(Guid usuarioId) => evaluaciones.GetValueOrDefault(usuarioId);
    public void Registrar(Guid usuarioId, string estado, string mensaje, int? candidatos = null) =>
        evaluaciones[usuarioId] = new(estado, mensaje, DateTime.UtcNow, candidatos);
}
