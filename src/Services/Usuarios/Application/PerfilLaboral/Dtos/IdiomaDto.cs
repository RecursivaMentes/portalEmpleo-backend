using System.ComponentModel.DataAnnotations;

namespace Application.PerfilLaboral.Dtos;

public record IdiomaDto(
    [param: Required, MaxLength(100)]
    string Idioma,

    [param: Required]
    string Nivel
);
