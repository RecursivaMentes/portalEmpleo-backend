namespace Application.Auth;

public record RegisterRequest(
    string Email,
    string Password,
    string Nombre,
    string Apellido
);
