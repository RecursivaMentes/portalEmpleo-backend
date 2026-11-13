using Application.Postulaciones;

namespace EmpleosYpostulaciones.Infrastructure.Services;

// El agente de Chatbot se integra en el Commit 9. Nunca simula envios.
public sealed class PreparadorIAEnEspera : IPreparadorPostulacionesIA
{
    public bool Disponible => false;

    public Task<ResultadoPreparacionIA> PrepararAsync(
        Guid usuarioId, Guid empleoId, CancellationToken ct = default) =>
        throw new InvalidOperationException("La preparacion con IA todavia no esta integrada.");
}
