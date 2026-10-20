using System.ComponentModel.DataAnnotations;

namespace Application.Auth;

public record RegisterRequest(
    [param: Required, EmailAddress, MaxLength(256)]
    string Email,

    [param: Required, MinLength(8), MaxLength(128)]
    string Password,

    [param: Required, MaxLength(100)]
    string Nombre,

    [param: Required, MaxLength(100)]
    string Apellido
);
