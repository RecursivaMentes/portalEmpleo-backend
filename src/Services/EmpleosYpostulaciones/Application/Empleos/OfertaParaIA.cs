using Domain.Enums;

namespace Application.Empleos;

public sealed record OfertaParaIA(Guid Id, string Titulo, string Descripcion,
    string? Requisitos, string? Ubicacion, ModalidadTrabajo Modalidad,
    TipoContrato TipoContrato, DateTime? FechaCierre);