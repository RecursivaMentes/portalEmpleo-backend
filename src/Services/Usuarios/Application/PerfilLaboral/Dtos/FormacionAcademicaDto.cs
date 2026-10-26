using System.ComponentModel.DataAnnotations;

namespace Application.PerfilLaboral.Dtos;

public record FormacionAcademicaDto(
    [param: Required, MaxLength(150)]
    string Institucion,

    [param: MaxLength(150)]
    string? Titulo,

    [param: Required]
    string NivelEducativo,

    [param: Required]
    string Estado,

    DateOnly? FechaInicio,

    DateOnly? FechaFin,

    [param: MaxLength(2000)]
    string? Descripcion
);
