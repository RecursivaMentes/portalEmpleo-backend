namespace Application.Auth;

public record UpdateProfileRequest(
    string? Nombre,
    string? Apellido,
    string? Telefono,
    string? PreferenciasJson,
    string? ReglasPostulacion
);
