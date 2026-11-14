using Application.Herramientas;

namespace Chatbot.Infrastructure.Clients;

public sealed class ConsultaEmpleosChatHttpClient(HttpClient http) : IConsultaEmpleosChat
{
    public Task<string> ConsultarAsync(string filtro, Guid? empleoId, CancellationToken ct = default)
    {
        var ruta = "/api/empleos";
        if (empleoId.HasValue)
            return BuscarIdAsync(ruta, empleoId.Value, ct);
        if (!string.IsNullOrWhiteSpace(filtro))
            ruta += "?busqueda=" + Uri.EscapeDataString(filtro.Trim());
        return http.GetStringAsync(ruta, ct);
    }

    // Se conserva el contrato publico existente: no hay endpoint nuevo obligatorio.
    private Task<string> BuscarIdAsync(string ruta, Guid id, CancellationToken ct) =>
        http.GetStringAsync(ruta, ct);
}