namespace Application.Empleos;

public class EmpleoService(IEmpleoRepository repositorio)
{
    public Task<List<EmpleoResultado>> BuscarEmpleosAsync(
        BuscarEmpleosFiltro filtros,
        CancellationToken cancellationToken = default)
    {
        return repositorio.BuscarPublicadosAsync(filtros, cancellationToken);
    }
}
