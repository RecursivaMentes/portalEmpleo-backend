using Domain.Enums;

namespace Application.Auth;

public record LoginResponse(
    Guid Id,
    string Email,
    string Nombre,
    string Apellido,
    RolUsuario Rol
);
