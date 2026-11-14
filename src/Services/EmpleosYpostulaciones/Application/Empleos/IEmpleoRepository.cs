namespace Application.Empleos;

public interface IEmpleoRepository
{
    Task<List<EmpleoResultado>> BuscarPublicadosAsync(
        BuscarEmpleosFiltro filtros,
        CancellationToken cancellationToken = default);

    Task<OfertaParaIA?> ObtenerParaIaAsync(Guid empleoId, CancellationToken ct = default) =>
        throw new NotSupportedException("Consulta interna de oferta no implementada.");
}
