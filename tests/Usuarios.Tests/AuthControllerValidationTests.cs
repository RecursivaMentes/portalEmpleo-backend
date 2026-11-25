using System.Security.Claims;
using Application.Auth;
using Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Usuarios.Api.Controllers;
using Xunit;

namespace Usuarios.Tests;

public class AuthControllerValidationTests
{
    private static AuthController CrearControlador()
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddControllers();
        var proveedor = servicios.BuildServiceProvider();

        var controller = new AuthController(new AuthService(
            new RepositorioUsuarioDePrueba(),
            new PasswordDePrueba(),
            new TokenDePrueba(),
            new ResetTokenDePrueba(),
            new EmailDePrueba()), new EntornoDePrueba());

        var http = new DefaultHttpContext { RequestServices = proveedor };
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", Guid.NewGuid().ToString())], "Pruebas"));
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    [Fact]
    public async Task ActualizarSinCampos_ConservaRespuestaBadRequest()
    {
        var controller = CrearControlador();
        var resultado = await controller.UpdateProfile(
            new UpdateProfileRequest(null, null, null, null, null),
            CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(resultado);
    }

    [Theory]
    [InlineData("{json invalido")]
    [InlineData("[]")]
    [InlineData("123")]
    public async Task PreferenciasJsonInvalido_ConservaRespuesta400(string json)
    {
        var controller = CrearControlador();
        var resultado = await controller.UpdateProfile(
            new UpdateProfileRequest(null, null, null, json, null),
            CancellationToken.None);

        var respuesta = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status400BadRequest, respuesta.StatusCode);
        var problema = Assert.IsType<ValidationProblemDetails>(respuesta.Value);
        Assert.Contains("PreferenciasJson", problema.Errors.Keys);
    }

    [Fact]
    public async Task PreferenciasJsonObjetoValido_ConservaRespuestaOk()
    {
        var controller = CrearControlador();
        var resultado = await controller.UpdateProfile(
            new UpdateProfileRequest(null, null, null, "{\"tema\":\"oscuro\"}", null),
            CancellationToken.None);
        Assert.IsType<OkObjectResult>(resultado);
    }

    private sealed class RepositorioUsuarioDePrueba : IUsuarioRepository
    {
        public Task<Usuario?> GetByEmailAsync(string email, CancellationToken ct = default)
            => Task.FromResult<Usuario?>(null);
        public Task<Usuario?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<Usuario?>(new Usuario { Id = id });
        public Task AddAsync(Usuario usuario, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task UpdateAsync(Usuario usuario, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task ActualizarPreferenciasLaboralesAsync(Guid usuarioId, string preferenciasJson,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> UpdatePasswordIfUnchangedAsync(Guid userId, string currentPasswordHash,
            string newPasswordHash, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class PasswordDePrueba : IPasswordService
    {
        public string HashPassword(string password) => password;
        public bool VerifyPassword(string hash, string password) => hash == password;
    }

    private sealed class TokenDePrueba : ITokenService
    {
        public TokenResult GenerateToken(Usuario usuario)
            => throw new NotSupportedException();
    }

    private sealed class ResetTokenDePrueba : IPasswordResetTokenService
    {
        public string GenerateToken(Guid userId, string passwordHash)
            => throw new NotSupportedException();
        public bool ValidateToken(string token, Guid userId, string passwordHash)
            => false;
    }

    private sealed class EmailDePrueba : IEmailService
    {
        public Task SendPasswordResetEmailAsync(string email, string resetLink,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class EntornoDePrueba : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Usuarios.Tests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = ".";
        public string WebRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
