using System.ComponentModel.DataAnnotations;

namespace Application.Auth;

public record UpdateProfileRequest(
    [param: RegularExpression(@"\A\p{L}[\p{L}\p{M}]*(?:[ '’-]\p{L}[\p{L}\p{M}]*)*\z", ErrorMessage = "Solo se permiten letras, espacios, guiones y apóstrofes.")]
    [param: MaxLength(100,ErrorMessage="Nombre demasiado largo")]
    [param: MinLength(1, ErrorMessage = "El nombre no puede estar vacío.")]
    string? Nombre,

    [param: RegularExpression(@"\A\p{L}[\p{L}\p{M}]*(?:[ '’-]\p{L}[\p{L}\p{M}]*)*\z", ErrorMessage = "Solo se permiten letras, espacios, guiones y apóstrofes.")]
    [param: MaxLength(100,ErrorMessage="Apellido demasiado largo")]
    [param: MinLength(1, ErrorMessage = "El apellido no puede estar vacío.")]
    string? Apellido,

    [param: MaxLength(30, ErrorMessage = "El teléfono no puede superar los 30 caracteres.")]
    [param: RegularExpression(@"\A(?=(?:[^0-9]*[0-9]){8,15}[^0-9]*\z)\+?(?:[0-9]+|\([0-9]+\))(?:[ -]?(?:[0-9]+|\([0-9]+\)))*\z", ErrorMessage = "El teléfono debe tener entre 8 y 15 dígitos y un formato válido.")]
    string? Telefono,

    [param: MaxLength(5000, ErrorMessage = "Las preferencias exceden el tamaño permitido.")]
    string? PreferenciasJson,

    [param: MaxLength(2000, ErrorMessage = "Las reglas de postulación no pueden superar los 2000 caracteres.")]
    string? ReglasPostulacion
);
