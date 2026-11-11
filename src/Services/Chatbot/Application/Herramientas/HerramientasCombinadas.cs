namespace Application.Herramientas;

/// <summary>Junta varios conjuntos de herramientas en uno solo: cada llamada va al que la define.</summary>
public class HerramientasCombinadas(params IEjecutorHerramientas[] ejecutores) : IEjecutorHerramientas
{
    public IReadOnlyList<DefinicionHerramienta> Definiciones { get; } =
        ejecutores.SelectMany(e => e.Definiciones).ToList();

    public Task<ResultadoHerramienta> EjecutarAsync(LlamadaHerramienta llamada, CancellationToken cancellationToken)
    {
        var ejecutor = ejecutores.FirstOrDefault(e => e.Definiciones.Any(d => d.Nombre == llamada.Nombre));

        return ejecutor is null
            ? Task.FromResult(new ResultadoHerramienta($"La herramienta '{llamada.Nombre}' no existe.", EsError: true))
            : ejecutor.EjecutarAsync(llamada, cancellationToken);
    }
}
