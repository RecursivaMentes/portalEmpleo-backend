namespace Application.Empleos;

public class EmpleoService(IEmpleoRepository repositorio)
{
    public Task<OfertaParaIA?> ObtenerParaIaAsync(Guid empleoId, CancellationToken ct = default)
    {
        if (empleoId == Guid.Empty) throw new ArgumentException("EmpleoId obligatorio.");
        return repositorio.ObtenerParaIaAsync(empleoId, ct);
    }

    public Task<List<EmpleoResultado>> BuscarEmpleosAsync(
        BuscarEmpleosFiltro filtros,
        CancellationToken cancellationToken = default)
    {
        return repositorio.BuscarPublicadosAsync(filtros, cancellationToken);
    }
}
