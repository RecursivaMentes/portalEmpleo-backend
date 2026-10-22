using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.PerfilLaboral.Dtos;

public record ExperienciaLaboralDto(
    [param: Required, MaxLength(150)]
    string Empresa,

    [param: Required, MaxLength(120)]
    string Puesto,

    [param: MaxLength(120)]
    string? Ubicacion,

    [param: MaxLength(2000)]
    string? Descripcion,

    [property: JsonRequired]
    DateOnly FechaInicio,

    DateOnly? FechaFin,

    [property: JsonRequired]
    bool EsActual
);
