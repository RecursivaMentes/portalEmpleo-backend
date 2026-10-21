using Domain.Enums;

namespace Application.Empleos;

public record EmpleoResultado(
    Guid Id,
    string Titulo,
    string Descripcion,
    string? Requisitos,
    string? Ubicacion,
    ModalidadTrabajo Modalidad,
    TipoContrato TipoContrato,
    decimal? SalarioMin,
    decimal? SalarioMax,
    DateTime FechaPublicacion,
    Guid EmpresaId
);
