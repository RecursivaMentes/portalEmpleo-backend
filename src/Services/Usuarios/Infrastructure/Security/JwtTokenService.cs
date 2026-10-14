using Application.Auth;
using Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Usuarios.Infrastructure.Security;

public class JwtTokenService(IConfiguration configuration)
    : ITokenService
{
    public TokenResult GenerateToken(Usuario usuario)
    {
        var issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Falta Jwt:Issuer");

        var audience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Falta Jwt:Audience");

        var secret = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Falta Jwt:Key");

        var minutes = configuration.GetValue<int>("Jwt:ExpirationMinutes");

        if (minutes <= 0)
            throw new InvalidOperationException("Vencimiento JWT inválido");

        var keyBytes = Convert.FromBase64String(secret);

        if (keyBytes.Length < 32)
            throw new InvalidOperationException("Clave JWT demasiado corta");

        var key = new SymmetricSecurityKey(keyBytes);

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim("role", usuario.Rol.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var expiration = DateTime.UtcNow.AddMinutes(minutes);

        var jwt = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiration,
            signingCredentials: credentials);

        var token = new JwtSecurityTokenHandler().WriteToken(jwt);

        return new TokenResult(token, expiration);
    }
}
