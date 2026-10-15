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

    public async Task<bool> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var usuarioExiste = await usuarios.GetByEmailAsync(
            request.Email,
            cancellationToken);

        if(usuarioExiste is not null)
            return false; //Email duplicado

        var nuevoUsuario = new Domain.Entities.Usuario
        {
            Id = Guid.NewGuid(),
            Email=request.Email,
            Nombre=request.Nombre,
            Apellido= request.Apellido,
            PreferenciasJson = "{}",
            Rol=Domain.Enums.RolUsuario.Postulante,
            FechaRegistro = DateTime.UtcNow,
            Estado=true
        };
        nuevoUsuario.PasswordHash = passwords.HashPassword(request.Password);
        await usuarios.AddAsync(nuevoUsuario, cancellationToken);

        return true;
    }
    public async Task<bool> UpdateAsync(Guid usuarioId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        //Busco usuario por su ID en Postgres
        var usuario = await usuarios.GetByIdAsync(usuarioId, cancellationToken); 

        if (usuario is null) 
            return false; // Usuario no encontrado

        if (request.Nombre is not null) usuario.Nombre = request.Nombre;
        if (request.Apellido is not null) usuario.Apellido = request.Apellido;
        if (request.Telefono is not null) usuario.Telefono = request.Telefono;
        if (request.PreferenciasJson is not null)usuario.PreferenciasJson = request.PreferenciasJson;
        if (request.ReglasPostulacion is not null)usuario.ReglasPostulacion = request.ReglasPostulacion;

        await usuarios.UpdateAsync(usuario, cancellationToken);
        return true;
    }
}
