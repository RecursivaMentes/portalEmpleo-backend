using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.Perfil;
using Microsoft.Extensions.Logging;

namespace Chatbot.Infrastructure.Clients;

/// <summary>
/// Consulta el perfil en el servicio Usuarios reenviando el token de la persona.
/// Lee con GET /api/auth/profile y GET /api/perfil-laboral/me, y escribe con
/// PUT /api/perfil-laboral/me (reemplaza el perfil laboral entero) y PUT /api/auth/profile (parcial).
/// </summary>
public class UsuariosClient(HttpClient http, ILogger<UsuariosClient> logger) : IPerfilUsuarioClient
{
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public async Task<PerfilUsuario?> ObtenerAsync(string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            var personalesTask = GetAsync<ProfileDto>("api/auth/profile", accessToken, cancellationToken);
            var laboralTask = GetAsync<PerfilLaboralDto>("api/perfil-laboral/me", accessToken, cancellationToken);

            await Task.WhenAll(personalesTask, laboralTask);

            var personales = personalesTask.Result;
            if (personales is null) return null;

            return new PerfilUsuario(
                personales.Nombre ?? string.Empty,
                personales.PreferenciasJson ?? "{}",
                personales.ReglasPostulacion,
                ToDominio(laboralTask.Result));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                   && !cancellationToken.IsCancellationRequested)
        {
            // El chat sigue funcionando sin el perfil.
            logger.LogWarning(ex, "No se pudo consultar el perfil en el servicio Usuarios.");
            return null;
        }
    }

    public Task<ResultadoEscritura> GuardarPerfilLaboralAsync(
        string accessToken, PerfilLaboralUsuario perfil, CancellationToken cancellationToken)
    {
        var cuerpo = new
        {
            resumen = perfil.Resumen,
            sinExperiencia = perfil.SinExperiencia,
            experiencias = perfil.Experiencias.Select(e => new
            {
                empresa = e.Empresa, puesto = e.Puesto, ubicacion = e.Ubicacion, descripcion = e.Descripcion,
                fechaInicio = e.FechaInicio, fechaFin = e.FechaFin, esActual = e.EsActual,
            }),
            formacion = perfil.Formacion.Select(f => new
            {
                institucion = f.Institucion, titulo = f.Titulo, nivelEducativo = f.NivelEducativo, estado = f.Estado,
                fechaInicio = f.FechaInicio, fechaFin = f.FechaFin, descripcion = f.Descripcion,
            }),
            habilidades = perfil.Habilidades.Select(h => new { nombre = h.Nombre, nivel = h.Nivel }),
            idiomas = perfil.Idiomas.Select(i => new { idioma = i.Idioma, nivel = i.Nivel }),
        };

        return PutAsync("api/perfil-laboral/me", accessToken, cuerpo, cancellationToken);
    }

    public Task<ResultadoEscritura> ActualizarDatosPersonalesAsync(
        string accessToken, ActualizacionDatosPersonales cambios, CancellationToken cancellationToken)
    {
        // Los campos en null no se modifican en Usuarios.
        var cuerpo = new
        {
            nombre = cambios.Nombre,
            apellido = cambios.Apellido,
            telefono = cambios.Telefono,
            preferenciasJson = cambios.PreferenciasJson,
        };

        return PutAsync("api/auth/profile", accessToken, cuerpo, cancellationToken);
    }

    private async Task<ResultadoEscritura> PutAsync(
        string ruta, string accessToken, object cuerpo, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, ruta)
            {
                Content = new StringContent(JsonSerializer.Serialize(cuerpo, OpcionesJson), Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await http.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode) return new ResultadoEscritura(true);

            // Los cuerpos pueden tener datos personales: se registra solo el estado, nunca el contenido.
            logger.LogWarning("Usuarios respondió {Status} en PUT {Ruta}.", (int)response.StatusCode, ruta);
            return new ResultadoEscritura(false, await LeerMensajeAsync(response, cancellationToken));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                   && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "No se pudo escribir en el servicio Usuarios (PUT {Ruta}).", ruta);
            return new ResultadoEscritura(false, "No se pudo comunicar con el servicio de perfiles.");
        }
    }

    /// <summary>Extrae el mensaje de error de Usuarios ({ "message": ... } o ProblemDetails).</summary>
    private static async Task<string> LeerMensajeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var generico = $"El servicio de perfiles respondió con el estado {(int)response.StatusCode}.";

        try
        {
            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);

            if (doc.RootElement.ValueKind != JsonValueKind.Object) return generico;

            if (doc.RootElement.TryGetProperty("message", out var mensaje) && mensaje.ValueKind == JsonValueKind.String)
                return mensaje.GetString() ?? generico;

            if (doc.RootElement.TryGetProperty("errors", out var errores) && errores.ValueKind == JsonValueKind.Object)
            {
                foreach (var campo in errores.EnumerateObject())
                    foreach (var texto in campo.Value.EnumerateArray())
                        if (texto.ValueKind == JsonValueKind.String)
                            return $"{campo.Name}: {texto.GetString()}";
            }

            if (doc.RootElement.TryGetProperty("title", out var titulo) && titulo.ValueKind == JsonValueKind.String)
                return titulo.GetString() ?? generico;
        }
        catch (JsonException) { }

        return generico;
    }

    /// <summary>Devuelve null si el endpoint responde 404 u otro error: se lo trata como "sin datos".</summary>
    private async Task<T?> GetAsync<T>(string ruta, string accessToken, CancellationToken cancellationToken)
        where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ruta);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await http.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound) return null;

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Usuarios respondió {Status} en GET {Ruta}.", (int)response.StatusCode, ruta);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<T>(OpcionesJson, cancellationToken);
    }

    private static PerfilLaboralUsuario? ToDominio(PerfilLaboralDto? dto)
    {
        if (dto is null) return null;

        return new PerfilLaboralUsuario(
            dto.Resumen,
            dto.SinExperiencia,
            (dto.Experiencias ?? []).Select(e => new ExperienciaUsuario(
                e.Empresa ?? "", e.Puesto ?? "", e.Ubicacion, e.Descripcion,
                e.FechaInicio, e.FechaFin, e.EsActual)).ToList(),
            (dto.Formacion ?? []).Select(f => new FormacionUsuario(
                f.Institucion ?? "", f.Titulo, f.NivelEducativo ?? "", f.Estado ?? "",
                f.FechaInicio, f.FechaFin, f.Descripcion)).ToList(),
            (dto.Habilidades ?? []).Select(h => new HabilidadUsuario(h.Nombre ?? "", h.Nivel)).ToList(),
            (dto.Idiomas ?? []).Select(i => new IdiomaUsuario(i.Idioma ?? "", i.Nivel ?? "")).ToList());
    }

    // Formato que expone Usuarios. Se declara acá, sin compartir código entre servicios, y las
    // fechas se leen como texto para no fallar si Usuarios cambia el formato.
    private record ProfileDto(string? Nombre, string? PreferenciasJson, string? ReglasPostulacion);

    private record PerfilLaboralDto(
        string? Resumen,
        bool SinExperiencia,
        List<ExperienciaDto>? Experiencias,
        List<FormacionDto>? Formacion,
        List<HabilidadDto>? Habilidades,
        List<IdiomaDto>? Idiomas);

    private record ExperienciaDto(
        string? Empresa, string? Puesto, string? Ubicacion, string? Descripcion,
        string? FechaInicio, string? FechaFin, bool EsActual);

    private record FormacionDto(
        string? Institucion, string? Titulo, string? NivelEducativo, string? Estado,
        string? FechaInicio, string? FechaFin, string? Descripcion);

    private record HabilidadDto(string? Nombre, string? Nivel);

    private record IdiomaDto(string? Idioma, string? Nivel);
}
