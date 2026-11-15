using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Application.Postulaciones;

namespace Usuarios.Api.Services;

public sealed record ResultadoReservaCambio(bool Permitido, Guid? OperacionId,
    Guid? PreparacionId, int? VersionEdicion, string? Estado);

// Delega el JWT autenticado de la solicitud, que vuelve a validarse en Postulaciones.
public sealed class ControlPostulacionesClient(HttpClient http, IConfiguration config,
    IHttpContextAccessor contexto)
{
    private HttpRequestMessage NuevaPeticion(HttpMethod metodo, string ruta)
    {
        var destino = config["Services:EmpleosYpostulaciones:BaseUrl"];
        if (string.IsNullOrWhiteSpace(destino))
            throw new InvalidOperationException("Falta configurar la conexión a Postulaciones.");
        var bearer = contexto.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(bearer) ||
            !bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("La sesión no está disponible.");
        var peticion = new HttpRequestMessage(metodo, new Uri(destino.TrimEnd('/') + ruta));
        peticion.Headers.Authorization = AuthenticationHeaderValue.Parse(bearer);
        return peticion;
    }

    public async Task<ResultadoReservaCambio> IniciarCambioAsync(Guid usuarioId, CancellationToken ct)
    {
        using var req = NuevaPeticion(HttpMethod.Post, "/api/postulaciones/control/cambios/iniciar");
        using var resp = await http.SendAsync(req, ct);
        if (resp.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Conflict))
        {
            resp.EnsureSuccessStatusCode();
            throw new HttpRequestException("Respuesta inválida al comprobar trabajo activo.");
        }
        return await resp.Content.ReadFromJsonAsync<ResultadoReservaCambio>(cancellationToken: ct)
            ?? throw new HttpRequestException("Respuesta vacía del control de trabajos activos.");
    }

    public async Task FinalizarCambioAsync(Guid usuarioId, Guid operacionId, CancellationToken ct)
    {
        using var req = NuevaPeticion(HttpMethod.Post,
            $"/api/postulaciones/control/cambios/{operacionId}/finalizar");
        using var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<ConfiguracionPostulaciones?> ObtenerAsync(Guid usuarioId, CancellationToken ct)
    {
        using var req = NuevaPeticion(HttpMethod.Get, "/api/postulaciones/control/configuracion");
        using var resp = await http.SendAsync(req, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ConfiguracionPostulaciones>(cancellationToken: ct);
    }

    public async Task GuardarAsync(Guid usuarioId, Guid operacionId,
        ConfiguracionPostulaciones configuracion, CancellationToken ct)
    {
        using var req = NuevaPeticion(HttpMethod.Put, "/api/postulaciones/control/configuracion");
        req.Headers.Add("X-Cambio-Operacion-Id", operacionId.ToString());
        req.Content = JsonContent.Create(configuracion);
        using var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
    }
}
