using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Application.Postulaciones;

namespace EmpleosYpostulaciones.Infrastructure.Clients;

// No replica el algoritmo de Recomendaciones ni la configuracion de Usuarios.
public sealed class DatosOrdenamientoHttpClient(IHttpClientFactory fabrica) : IDatosExternosOrdenamiento
{
    private sealed record ConfiguracionDto(string? OrdenPendientes, PreferenciasParaPrioridad? Preferencias);
    private sealed record CompatibilidadDto(Guid EmpleoId, decimal Score);

    public async Task<DatosExternosOrdenamiento> ObtenerAsync(Guid usuarioId, string accessToken,
        CancellationToken ct = default)
    {
        try
        {
            var usuario = fabrica.CreateClient("UsuariosOrdenPendientes");
            var recomendaciones = fabrica.CreateClient("RecomendacionesOrdenPendientes");
            using var reqConfig = new HttpRequestMessage(HttpMethod.Get, "/api/postulaciones/configuracion/me");
            using var reqCompat = new HttpRequestMessage(HttpMethod.Get, $"/api/recomendaciones/analizar/{usuarioId}");
            reqConfig.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            reqCompat.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var configResponse = await usuario.SendAsync(reqConfig, ct);
            if (configResponse.StatusCode == HttpStatusCode.Unauthorized ||
                configResponse.StatusCode == HttpStatusCode.Forbidden)
                throw PostulacionException.SesionNoValida();
            configResponse.EnsureSuccessStatusCode();
            var configuracion = await configResponse.Content.ReadFromJsonAsync<ConfiguracionDto>(cancellationToken: ct)
                ?? throw PostulacionException.DatosExternosInvalidos();

            using var compatResponse = await recomendaciones.SendAsync(reqCompat, ct);
            if (compatResponse.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw PostulacionException.SesionNoValida();
            List<CompatibilidadDto> analisis = [];
            // Un perfil aun inexistente no impide ver los pendientes: no se inventa la compatibilidad.
            if (compatResponse.StatusCode != HttpStatusCode.NotFound)
            {
                compatResponse.EnsureSuccessStatusCode();
                analisis = await compatResponse.Content.ReadFromJsonAsync<List<CompatibilidadDto>>(
                    cancellationToken: ct) ?? throw PostulacionException.DatosExternosInvalidos();
            }

            var scores = analisis.GroupBy(x => x.EmpleoId)
                .ToDictionary(x => x.Key, x => x.First().Score);
            return new DatosExternosOrdenamiento(
                configuracion.OrdenPendientes ?? "Prioridad", configuracion.Preferencias, scores);
        }
        catch (HttpRequestException ex)
        {
            throw PostulacionException.OrdenamientoNoDisponible(ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw PostulacionException.OrdenamientoNoDisponible(ex);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw PostulacionException.OrdenamientoNoDisponible(ex);
        }
    }
}
