using System.ComponentModel.DataAnnotations;

namespace Application.Auth;

public record LoginRequest(
    [param: Required(ErrorMessage = "Debe ingresar su correo electrónico.")]
    [param: EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido.")]
    [param: MaxLength(256, ErrorMessage = "El correo electrónico es demasiado largo.")]
    string Email,

    [param: Required(ErrorMessage = "Debe ingresar una contraseña.")]
    [param: MaxLength(128, ErrorMessage = "La contraseña es demasiado larga.")]
    string Password
);