using Application.PerfilLaboral.Dtos;

namespace Application.PerfilLaboral.Responses;

public record PerfilLaboralResponse(
    Guid Id,
    string? Resumen,
    List<ExperienciaLaboralDto>? Experiencias,
    bool SinExperiencia,
    List<FormacionAcademicaDto> Formacion,
    List<HabilidadDto> Habilidades,
    List<IdiomaDto> Idiomas,
    DateTime FechaActualizacion
);
