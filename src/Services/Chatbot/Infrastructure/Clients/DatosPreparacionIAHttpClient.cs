using System.Net;
using System.Text.Json;
using Application.Postulaciones;
using Microsoft.Extensions.Configuration;

namespace Chatbot.Infrastructure.Clients;

// Solo Infrastructure conoce rutas, credenciales internas y transporte HTTP.
public sealed class DatosPreparacionIAHttpClient(IHttpClientFactory fabrica,
    IConfiguration configuracion) : IDatosPreparacionIA
{
    private async Task<JsonElement?> LeerAsync(string cliente, string ruta, CancellationToken ct)
    {
        var clave = configuracion["InternalServices:Chatbot:Key"];
        if (string.IsNullOrWhiteSpace(clave))
            throw new InvalidOperationException("No está configurada la autenticación interna de Chatbot.");
        using var req = new HttpRequestMessage(HttpMethod.Get, ruta);
        req.Headers.Add("X-Internal-Service", "Chatbot");
        req.Headers.Add("X-Internal-Key", clave);
        try
        {
            using var resp = await fabrica.CreateClient(cliente).SendAsync(req, ct);
            if (resp.StatusCode == HttpStatusCode.NotFound) return null;
            resp.EnsureSuccessStatusCode();
            using var json = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct),
                cancellationToken: ct);
            return json.RootElement.Clone();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException &&
                                   !ct.IsCancellationRequested)
        {
            var servicio = cliente == "UsuariosIA" ? "Usuarios" : "EmpleosYpostulaciones";
            throw new DatosPreparacionIANoDisponiblesException(servicio, ex);
        }
    }

    public Task<JsonElement?> PerfilAsync(Guid usuarioId, CancellationToken ct) =>
        LeerAsync("UsuariosIA", $"/internal/usuarios/{usuarioId}/ia/perfil", ct);
    public Task<JsonElement?> CvsAsync(Guid usuarioId, CancellationToken ct) =>
        LeerAsync("UsuariosIA", $"/internal/usuarios/{usuarioId}/ia/cvs", ct);
    public Task<JsonElement?> CvAsync(Guid usuarioId, Guid cvId, CancellationToken ct) =>
        LeerAsync("UsuariosIA", $"/internal/usuarios/{usuarioId}/ia/cvs/{cvId}", ct);
    public Task<JsonElement?> OfertaAsync(Guid empleoId, CancellationToken ct) =>
        LeerAsync("EmpleosIA", $"/internal/empleos/{empleoId}/para-ia", ct);
}