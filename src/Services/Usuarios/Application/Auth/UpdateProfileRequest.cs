using System.ComponentModel.DataAnnotations;

namespace Application.Auth;

public record UpdateProfileRequest(
    [param: RegularExpression(@"^(?=.*\p{L})[\p{L}\s]+$", ErrorMessage = "Solo se permiten letras y espacios.")]
    [param: MaxLength(100,ErrorMessage="Nombre demasiado largo")]
    [param: MinLength(1, ErrorMessage = "El nombre no puede estar vacío.")]
    string? Nombre,

    [param: RegularExpression(@"^(?=.*\p{L})[\p{L}\s]+$", ErrorMessage = "Solo se permiten letras y espacios.")]
    [param: MaxLength(100,ErrorMessage="Apellido demasiado largo")]
    [param: MinLength(1, ErrorMessage = "El apellido no puede estar vacío.")]
    string? Apellido,

    [param: MaxLength(30, ErrorMessage = "El teléfono no puede superar los 30 caracteres.")]
    [param: RegularExpression(@"^[0-9+\-\s()]*$", ErrorMessage = "El formato del teléfono no es válido.")]
    string? Telefono,

    [param: MaxLength(5000, ErrorMessage = "Las preferencias exceden el tamaño permitido.")]
    string? PreferenciasJson,

    [param: MaxLength(2000, ErrorMessage = "Las reglas de postulación no pueden superar los 2000 caracteres.")]
    string? ReglasPostulacion
);
