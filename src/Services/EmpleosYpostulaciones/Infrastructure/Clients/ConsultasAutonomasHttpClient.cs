using System.Net;
using System.Net.Http.Json;
using Application.Postulaciones;
using Microsoft.Extensions.Configuration;

namespace EmpleosYpostulaciones.Infrastructure.Clients;

public sealed class ConsultasAutonomasHttpClient(IHttpClientFactory fabrica, IConfiguration opciones)
    : IConsultasProcesamientoInterno, IDatosExternosOrdenamientoInterno
{
    private sealed record CompatibilidadInterna(Guid EmpleoId, decimal Score);

    private static HttpRequestMessage CrearSolicitud(string ruta, IConfiguration opciones)
    {
        var clave = opciones["InternalServices:EmpleosYpostulaciones:Key"];
        if (string.IsNullOrWhiteSpace(clave))
            throw new InvalidOperationException("Falta la credencial interna de EmpleosYpostulaciones.");
        var request = new HttpRequestMessage(HttpMethod.Get, ruta);
        request.Headers.Add("X-Internal-Service", "EmpleosYpostulaciones");
        request.Headers.Add("X-Internal-Key", clave);
        return request;
    }

    private async Task<HttpResponseMessage> ConsultarAsync(string cliente, string ruta, CancellationToken ct)
    {
        using var solicitud = CrearSolicitud(ruta, opciones);
        var respuesta = await fabrica.CreateClient(cliente).SendAsync(solicitud, ct);
        if (respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            respuesta.Dispose();
            throw new HttpRequestException("Credencial interna rechazada.", null, respuesta.StatusCode);
        }
        return respuesta;
    }

    public async Task<ConfiguracionProcesamiento?> ConfiguracionAsync(Guid usuarioId,
        CancellationToken ct = default)
    {
        using var respuesta = await ConsultarAsync("UsuariosOrdenPendientes",
            $"/internal/usuarios/{usuarioId}/postulaciones/configuracion", ct);
        if (respuesta.StatusCode == HttpStatusCode.NotFound) return null;
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.Content.ReadFromJsonAsync<ConfiguracionProcesamiento>(cancellationToken: ct)
            ?? throw new System.Text.Json.JsonException("Configuracion interna vacia.");
    }

    public async Task<ValidacionPerfilLaboralResultado?> ValidarPerfilAsync(Guid usuarioId,
        CancellationToken ct = default)
    {
        using var respuesta = await ConsultarAsync("UsuariosOrdenPendientes",
            $"/internal/usuarios/{usuarioId}/perfil-laboral/validacion", ct);
        if (respuesta.StatusCode == HttpStatusCode.NotFound) return null;
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.Content.ReadFromJsonAsync<ValidacionPerfilLaboralResultado>(cancellationToken: ct)
            ?? throw new System.Text.Json.JsonException("Validacion interna vacia.");
    }

    public async Task<bool> ValidarCvAsync(Guid usuarioId, Guid cvId, CancellationToken ct = default)
    {
        using var respuesta = await ConsultarAsync("UsuariosOrdenPendientes",
            $"/internal/usuarios/{usuarioId}/cvs/{cvId}/validacion", ct);
        if (respuesta.StatusCode == HttpStatusCode.NotFound) return false;
        respuesta.EnsureSuccessStatusCode();
        return respuesta.StatusCode == HttpStatusCode.NoContent;
    }

    public async Task<DatosExternosOrdenamiento> ObtenerInternoAsync(Guid usuarioId,
        CancellationToken ct = default)
    {
        var configuracion = await ConfiguracionAsync(usuarioId, ct)
            ?? throw new InvalidOperationException("Cuenta no disponible para ordenamiento interno.");
        using var respuesta = await ConsultarAsync("RecomendacionesOrdenPendientes",
            $"/internal/recomendaciones/usuarios/{usuarioId}/analizar", ct);
        List<CompatibilidadInterna> resultados = [];
        if (respuesta.StatusCode != HttpStatusCode.NotFound)
        {
            try { respuesta.EnsureSuccessStatusCode(); }
            catch (HttpRequestException ex)
            {
                throw new ServicioDependienteException("Recomendaciones",
                    "No fue posible consultar las compatibilidades.", ex);
            }
            resultados = await respuesta.Content.ReadFromJsonAsync<List<CompatibilidadInterna>>(
                cancellationToken: ct) ?? throw new System.Text.Json.JsonException("Compatibilidades vacias.");
        }
        var pref = configuracion.Preferencias;
        return new DatosExternosOrdenamiento(
            configuracion.OrdenPendientes ?? "Prioridad",
            pref is null ? null : new PreferenciasParaPrioridad(
                pref.PuestosInteres, pref.UbicacionesLaborales,
                pref.ModalidadesTrabajo, pref.TiposContrato, pref.ExpectativaSalarial),
            resultados.GroupBy(x => x.EmpleoId).ToDictionary(x => x.Key, x => x.First().Score));
    }
}
