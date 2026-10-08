namespace Application.Auth;

public class AuthService(IUsuarioRepository usuarios)
{
    public async Task<LoginResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var usuario = await usuarios.GetByEmailAsync(
            request.Email,
            cancellationToken);

        if (usuario is null || !usuario.Estado)
            return null;

        if (usuario.PasswordHash != request.Password)
            return null;

        return new LoginResponse(
            usuario.Id,
            usuario.Email,
            usuario.Nombre,
            usuario.Apellido,
            usuario.Rol);
    }
}
