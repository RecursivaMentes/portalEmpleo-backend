using Application.Auth;
using Domain.Entities;
namespace Application.Auth;

public class AuthService(
    IUsuarioRepository usuarios,
    IPasswordService passwords,
    ITokenService tokens)
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

        if (!passwords.VerifyPassword(
            usuario.PasswordHash,
            request.Password))
            return null;

        var result = tokens.GenerateToken(usuario);

        return new LoginResponse(
            result.AccessToken,
            result.ExpiresAtUtc);
    }
}
