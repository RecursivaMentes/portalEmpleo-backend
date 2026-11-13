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

    // Consultas autenticadas entre microservicios; no dependen del JWT del usuario.
    public async Task<List<CompatibilidadResponse>?> AnalizarInternoAsync(
        Guid usuarioId, string claveInterna, CancellationToken ct = default)
    {
        var perfil = await _usuariosClient.ObtenerPerfilInternoAsync(usuarioId, claveInterna, ct);
        if (perfil is null) return null;
        var habilidades = perfil.Habilidades
            .Where(h => h.TryGetValue("nombre", out var nombre) && !string.IsNullOrWhiteSpace(nombre))
            .Select(h => h["nombre"]!.Trim()).ToList();
        var empleos = await _empleosClient.ObtenerEmpleosActivosAsync(ct);
        var resultados = new List<CompatibilidadResponse>();
        foreach (var empleo in empleos)
        {
            if (string.IsNullOrWhiteSpace(empleo.Requisitos)) continue;
            var resultado = _compatibilidadService.Analizar(new AnalizarCompatibilidadRequest
            {
                UsuarioId = usuarioId, EmpleoId = empleo.Id,
                Habilidades = habilidades, Requisitos = empleo.Requisitos
            });
            resultado.Titulo = empleo.Titulo;
            resultados.Add(resultado);
        }
        return resultados.OrderByDescending(x => x.Score).ToList();
    }

    public async Task<(CompatibilidadResponse? Resultado, string? Error)> AnalizarEmpleoInternoAsync(
        Guid usuarioId, Guid empleoId, string claveInterna, CancellationToken ct = default)
    {
        var perfil = await _usuariosClient.ObtenerPerfilInternoAsync(usuarioId, claveInterna, ct);
        if (perfil is null) return (null, "PERFIL_NO_ENCONTRADO");
        var empleo = (await _empleosClient.ObtenerEmpleosActivosAsync(ct))
            .FirstOrDefault(x => x.Id == empleoId);
        if (empleo is null) return (null, "EMPLEO_NO_ENCONTRADO");
        if (string.IsNullOrWhiteSpace(empleo.Requisitos)) return (null, "REQUISITOS_INSUFICIENTES");
        var habilidades = perfil.Habilidades
            .Where(h => h.TryGetValue("nombre", out var nombre) && !string.IsNullOrWhiteSpace(nombre))
            .Select(h => h["nombre"]!.Trim()).ToList();
        var resultado = _compatibilidadService.Analizar(new AnalizarCompatibilidadRequest
        {
            UsuarioId = usuarioId, EmpleoId = empleo.Id,
            Habilidades = habilidades, Requisitos = empleo.Requisitos
        });
        resultado.Titulo = empleo.Titulo;
        return (resultado, null);
    }

    // Analiza solo el empleo solicitado, reutilizando el mismo calculo de compatibilidad.
    // La API de Empleos expone actualmente un listado publico; se localiza el empleo
    // por Id, pero NO se recalculan puntuaciones para otras ofertas.
    public async Task<(CompatibilidadResponse? Resultado, string? Error)> AnalizarEmpleoAsync(
        Guid usuarioId,
        Guid empleoId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var perfil = await _usuariosClient.ObtenerPerfilAsync(accessToken, cancellationToken);
        if (perfil is null)
            return (null, "PERFIL_NO_ENCONTRADO");

        var empleos = await _empleosClient.ObtenerEmpleosActivosAsync(cancellationToken);
        var empleo = empleos.FirstOrDefault(e => e.Id == empleoId);
        if (empleo is null)
            return (null, "EMPLEO_NO_ENCONTRADO");

        if (string.IsNullOrWhiteSpace(empleo.Requisitos))
            return (null, "REQUISITOS_INSUFICIENTES");

        var habilidades = perfil.Habilidades
            .Where(h => h.TryGetValue("nombre", out var nombre) && !string.IsNullOrWhiteSpace(nombre))
            .Select(h => h["nombre"]!.Trim())
            .ToList();

        var resultado = _compatibilidadService.Analizar(new AnalizarCompatibilidadRequest
        {
            UsuarioId = usuarioId,
            EmpleoId = empleo.Id,
            Habilidades = habilidades,
            Requisitos = empleo.Requisitos
        });
        resultado.Titulo = empleo.Titulo;
        return (resultado, null);
    }
}
