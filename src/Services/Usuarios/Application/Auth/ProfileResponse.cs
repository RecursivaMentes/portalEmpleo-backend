namespace Application.Auth;

public record ProfileResponse(
    Guid Id,
    string Email,
    string Nombre,
    string Apellido,
    string? Telefono,
    string PreferenciasJson,
    string? ReglasPostulacion
);
