using System.ComponentModel.DataAnnotations;

namespace Application.Auth;

public record RegisterRequest(
    [param: Required(ErrorMessage = "Debe ingresar su correo electrónico .")]
    [param: EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido.")]
    [param: MaxLength(256, ErrorMessage = "El correo electrónico es demasiado largo.")]
    string Email,

    [param: Required(ErrorMessage = "Debe ingresar una contraseña.")]
    [param: MinLength(PasswordPolicy.MinLength,ErrorMessage = "Debe ingresar al menos 8 caracteres.")]
    [param: MaxLength(PasswordPolicy.MaxLength, ErrorMessage = "La contraseña es demasiado larga.")]
    string Password,

    [param: Required(ErrorMessage = "Debe ingresar su nombre.")]
    [param: RegularExpression(@"\A\p{L}[\p{L}\p{M}]*(?:[ '’-]\p{L}[\p{L}\p{M}]*)*\z", ErrorMessage = "Solo se permiten letras, espacios, guiones y apóstrofes.")]
    [param: MaxLength(100, ErrorMessage = "Nombre demasiado largo.")]
    string Nombre,

    [param: Required(ErrorMessage = "Debe ingresar su apellido.")]
    [param: RegularExpression(@"\A\p{L}[\p{L}\p{M}]*(?:[ '’-]\p{L}[\p{L}\p{M}]*)*\z", ErrorMessage = "Solo se permiten letras, espacios, guiones y apóstrofes.")]
    [param: MaxLength(100, ErrorMessage = "Apellido demasiado largo.")]
    string Apellido
);
