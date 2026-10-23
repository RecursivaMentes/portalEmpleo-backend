using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Application.PerfilLaboral.Dtos;

namespace Application.PerfilLaboral.Requests;

public record GuardarPerfilLaboralRequest(
    [param: MaxLength(2000)]
    string? Resumen,

    List<ExperienciaLaboralDto>? Experiencias,

    [property: JsonRequired]
    bool SinExperiencia,

    List<FormacionAcademicaDto>? Formacion,
    List<HabilidadDto>? Habilidades,
    List<IdiomaDto>? Idiomas
);
