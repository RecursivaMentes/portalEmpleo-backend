using System.ComponentModel.DataAnnotations;

namespace Application.PerfilLaboral.Dtos;

public record HabilidadDto(
    [param: Required, MaxLength(100)]
    string Nombre,

    string? Nivel
);
