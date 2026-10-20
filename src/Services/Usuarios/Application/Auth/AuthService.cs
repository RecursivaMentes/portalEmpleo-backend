using System.Data;
using Domain.Enums;
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

    public async Task<bool> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await usuarios.GetByEmailAsync(email, cancellationToken) is not null)
            return false;

        var nuevoUsuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Email = email,
            Nombre = request.Nombre.Trim(),
            Apellido = request.Apellido.Trim(),
            PreferenciasJson = "{}",
            Rol = RolUsuario.Postulante,
            FechaRegistro = DateTime.UtcNow,
            Estado = true,
            PasswordHash = passwords.HashPassword(request.Password)
        };

        try
        {
            await usuarios.AddAsync(nuevoUsuario, cancellationToken);
            return true;
        }
        catch (DuplicateNameException)
        {
            return false;
        }
    }
    public async Task<bool> UpdateAsync(
        Guid usuarioId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var usuario = await usuarios.GetByIdAsync(usuarioId, cancellationToken);
        if (usuario is null)
            return false;

        if (request.Nombre is not null)
            usuario.Nombre = request.Nombre.Trim();
        if (request.Apellido is not null)
            usuario.Apellido = request.Apellido.Trim();
        if (request.Telefono is not null)
            usuario.Telefono = request.Telefono;
        if (request.PreferenciasJson is not null)
            usuario.PreferenciasJson = request.PreferenciasJson;
        if (request.ReglasPostulacion is not null)
            usuario.ReglasPostulacion = request.ReglasPostulacion;

        await usuarios.UpdateAsync(usuario, cancellationToken);
        return true;
    }

    public async Task<ProfileResponse?> GetByIdAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var usuario = await usuarios.GetByIdAsync(usuarioId, cancellationToken);
        if (usuario is null)
            return null;

        return new ProfileResponse(
            usuario.Id,
            usuario.Email,
            usuario.Nombre,
            usuario.Apellido,
            usuario.Telefono,
            usuario.PreferenciasJson,
            usuario.ReglasPostulacion);
    }
}
