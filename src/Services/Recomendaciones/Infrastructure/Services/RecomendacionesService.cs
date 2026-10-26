using Recomendaciones.Application.DTOs;

namespace Recomendaciones.Infrastructure.Services;

public class RecomendacionesService
{
    private readonly AnalisisOfertasService _analisisOfertasService;

    public RecomendacionesService(
        AnalisisOfertasService analisisOfertasService)
    {
        _analisisOfertasService = analisisOfertasService;
    }

    public async Task<List<RecomendacionLaboralDto>?> ObtenerRecomendacionesAsync(
        Guid usuarioId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var analisis = await _analisisOfertasService.AnalizarAsync(
            usuarioId, accessToken, cancellationToken);

        if (analisis is null)
            return null;

        var recomendaciones = analisis
            .Select(resultado => new RecomendacionLaboralDto
            {
                EmpleoId = resultado.EmpleoId,
                Titulo = resultado.Titulo,
                PorcentajeCompatibilidad = resultado.Score,
                Coincidencias = resultado.Coincidencias,
                RequisitosFaltantes = resultado.RequisitosFaltantes,
                Motivo = resultado.Motivo
            })
            .OrderByDescending(r => r.PorcentajeCompatibilidad)
            .ToList();

        return recomendaciones;
    }
}
