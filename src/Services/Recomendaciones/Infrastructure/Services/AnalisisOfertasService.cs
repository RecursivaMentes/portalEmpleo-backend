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
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var perfil = await _usuariosClient.ObtenerPerfilAsync(
            accessToken, cancellationToken);

        if (perfil is null)
            return null;

        var habilidades = perfil.Habilidades
            .Where(h => h.TryGetValue("nombre", out var nombre) && !string.IsNullOrWhiteSpace(nombre))
            .Select(h => h["nombre"]!.Trim())
            .ToList();

        var empleos = await _empleosClient.ObtenerEmpleosActivosAsync(
            cancellationToken);

        var resultados = new List<CompatibilidadResponse>();

        foreach (var empleo in empleos)
        {
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
