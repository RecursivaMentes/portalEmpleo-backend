using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Perfil;
using Microsoft.Extensions.Logging;

namespace Chatbot.Infrastructure.Clients;

/// <summary>
/// Consulta el perfil en el servicio Usuarios reenviando el token de la persona.
/// Usa dos endpoints: GET /api/auth/profile y GET /api/perfil-laboral/me.
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
                f.FechaInicio, f.FechaFin)).ToList(),
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
        string? FechaInicio, string? FechaFin);

    private record HabilidadDto(string? Nombre, string? Nivel);

    private record IdiomaDto(string? Idioma, string? Nivel);
}
