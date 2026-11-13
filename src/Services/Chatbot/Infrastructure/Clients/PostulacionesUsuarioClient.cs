using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Herramientas;

namespace Chatbot.Infrastructure.Clients;

public sealed class PostulacionesUsuarioClient(HttpClient http) : IPostulacionesUsuarioClient
{
    public async Task<ResultadoSolicitudPostulacionIA> SolicitarAsync(string accessToken,
        Guid empleoId, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post,
                "/api/postulaciones/preparaciones/solicitar-con-ia");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            req.Content = JsonContent.Create(new { empleoId });
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
                return new(false, "Rechazada", $"Postulaciones rechazÃ³ la solicitud (HTTP {(int)resp.StatusCode}). RevisÃ¡ el detalle o la preparaciÃ³n existente.");
            using var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var estado = json.RootElement.TryGetProperty("estado", out var e)
                ? e.ToString() : "Registrada";
            return new(true, estado, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new(false, "NoDisponible", "El servicio de postulaciones no estÃ¡ disponible.");
        }
    }
}