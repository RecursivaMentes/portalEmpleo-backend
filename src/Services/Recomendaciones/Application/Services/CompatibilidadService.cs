using Recomendaciones.Application.DTOs;

namespace Recomendaciones.Application.Services;

public class CompatibilidadService
{
    public CompatibilidadResponse Analizar(AnalizarCompatibilidadRequest request)
    {
        var habilidades = (request.Habilidades ?? new List<string>())
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Select(h => h.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var requisitos = (request.Requisitos ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var coincidencias = requisitos
            .Where(r => habilidades.Contains(r, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var requisitosFaltantes = requisitos
            .Where(r => !habilidades.Contains(r, StringComparer.OrdinalIgnoreCase))
            .ToList();

        decimal score = requisitos.Count == 0
            ? 0
            : Math.Round(
                (decimal)coincidencias.Count / requisitos.Count * 100,
                2
            );

        return new CompatibilidadResponse
        {
            UsuarioId = request.UsuarioId,
            EmpleoId = request.EmpleoId,
            Score = score,
            Coincidencias = coincidencias,
            RequisitosFaltantes = requisitosFaltantes,
            Motivo = $"El usuario cumple {coincidencias.Count} de {requisitos.Count} requisitos."
        };
    }
}
