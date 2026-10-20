namespace Application.Empleos;

public interface IEmpleoRepository
{
    Task<List<EmpleoResultado>> BuscarPublicadosAsync(
        BuscarEmpleosFiltro filtros,
        CancellationToken cancellationToken = default);
}
