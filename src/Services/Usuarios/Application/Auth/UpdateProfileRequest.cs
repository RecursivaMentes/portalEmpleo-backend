using System.ComponentModel.DataAnnotations;

namespace Application.Auth;

public record UpdateProfileRequest(
    [param: MaxLength(100)]
    string? Nombre,

    [param: MaxLength(100)]
    string? Apellido,

    [param: MaxLength(30)]
    string? Telefono,

    string? PreferenciasJson,

    [param: MaxLength(2000)]
    string? ReglasPostulacion
);
