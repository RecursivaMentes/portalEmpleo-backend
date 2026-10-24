using System.Text.Json;
using Recomendaciones.Application.DTOs;
using Recomendaciones.Application.Services;
using Recomendaciones.Infrastructure.Clients;

namespace Recomendaciones.Infrastructure.Services;

public class AnalisisOfertasService
{
    private readonly UsuariosClient _usuariosClient;
    private readonly EmpleosClient _empleosClient;
    private readonly CompatibilidadService _compatibilidadService;

    public AnalisisOfertasService(
        UsuariosClient usuariosClient,
        EmpleosClient empleosClient,
        CompatibilidadService compatibilidadService)
    {
        _usuariosClient = usuariosClient;
        _empleosClient = empleosClient;
        _compatibilidadService = compatibilidadService;
    }

    public async Task<List<CompatibilidadResponse>?> AnalizarAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var perfil = await _usuariosClient.ObtenerPerfilAsync(
            usuarioId, cancellationToken);

        if (perfil is null)
            return null;

        List<string> habilidades;

        try
        {
            habilidades = JsonSerializer.Deserialize<List<string>>(
                perfil.HabilidadesJson) ?? new List<string>();
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(
                "El perfil laboral contiene un formato de habilidades inválido.");
        }

        var empleos = await _empleosClient.ObtenerEmpleosActivosAsync(
            cancellationToken);

        var ahora = DateTime.UtcNow;
        var resultados = new List<CompatibilidadResponse>();

        foreach (var empleo in empleos)
        {
            if (empleo.Estado != 1)
                continue;

            if (empleo.FechaCierre.HasValue &&
                empleo.FechaCierre.Value.ToUniversalTime() <= ahora)
                continue;

            if (string.IsNullOrWhiteSpace(empleo.Requisitos))
                continue;

            var request = new AnalizarCompatibilidadRequest
            {
                UsuarioId = usuarioId,
                EmpleoId = empleo.Id,
                Habilidades = habilidades,
                Requisitos = empleo.Requisitos
            };

            var resultado = _compatibilidadService.Analizar(request);
            resultado.Titulo = empleo.Titulo;
            resultados.Add(resultado);
        }

        return resultados
            .OrderByDescending(r => r.Score)
            .ToList();
    }
}
