using Domain.Enums;

namespace Application.Empleos;

public record BuscarEmpleosFiltro(
    string? Busqueda,
    string? Ubicacion,
    ModalidadTrabajo? Modalidad,
    TipoContrato? TipoContrato,
    decimal? SalarioMin,
    string? Orden
);
