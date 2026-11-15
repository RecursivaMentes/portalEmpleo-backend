using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Application.Auth;
using Xunit;

namespace Usuarios.Tests;

public class AuthRequestValidationTests
{
    // En los records, los atributos [param: ...] están sobre el constructor primario.
    // Se validan los atributos reales del DTO, sin duplicar sus reglas en los tests.
    private static IReadOnlyList<ValidationResult> Validar<T>(T request) where T : notnull
    {
        var errores = new List<ValidationResult>();
        var constructor = typeof(T).GetConstructors().Single();

        foreach (var parametro in constructor.GetParameters())
        {
            var propiedad = typeof(T).GetProperty(parametro.Name!,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)!;
            var atributos = parametro.GetCustomAttributes<ValidationAttribute>();
            Validator.TryValidateValue(propiedad.GetValue(request),
                new ValidationContext(request) { MemberName = propiedad.Name },
                errores, atributos);
        }
        return errores;
    }

    private static RegisterRequest Registro(
        string nombre = "María Jose", string apellido = "Pérez",
        string email = "maria@example.com", string password = "12345678") =>
        new(email, password, nombre, apellido);

    [Theory]
    [InlineData("María Jose")]
    [InlineData("Ana María")]
    [InlineData("María-José")]
    [InlineData("O'Connor")]
    [InlineData("O’Connor")]
    [InlineData("José")]
    public void Registro_AceptaNombresCompuestos(string nombre)
    {
        Assert.Empty(Validar(Registro(nombre: nombre)));
        Assert.Empty(Validar(Registro(apellido: nombre)));
    }

    [Theory]
    [InlineData("María\tJose")]
    [InlineData("María\nJose")]
    [InlineData("María  Jose")]
    [InlineData("123")]
    [InlineData("Juan1")]
    [InlineData(" Juan")]
    [InlineData("Juan ")]
    [InlineData("Juan@")]
    public void Registro_RechazaNombresInvalidos(string nombre)
    {
        Assert.NotEmpty(Validar(Registro(nombre: nombre)));
        Assert.NotEmpty(Validar(Registro(apellido: nombre)));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("correo-sin-arroba")]
    [InlineData("sin-dominio@")]
    public void Registro_RechazaEmailInvalido(string email)
    {
        Assert.NotEmpty(Validar(Registro(email: email)));
    }

    [Fact]
    public void Registro_RechazaEmailDemasiadoLargo()
    {
        Assert.NotEmpty(Validar(Registro(email: new string('a', 250) + "@example.com")));
    }

    [Fact]
    public void Registro_RespetaLongitudesDeContrasenaYNombres()
    {
        Assert.NotEmpty(Validar(Registro(password: "1234567")));
        Assert.Empty(Validar(Registro(password: "12345678")));
        Assert.NotEmpty(Validar(Registro(password: new string('x', 129))));
        Assert.Empty(Validar(Registro(nombre: new string('A', 100))));
        Assert.NotEmpty(Validar(Registro(nombre: new string('A', 101))));
        Assert.NotEmpty(Validar(Registro(apellido: "")));
    }

    [Fact]
    public void Registro_RechazaCamposObligatoriosNulos()
    {
        Assert.NotEmpty(Validar(new RegisterRequest(null!, "12345678", "Ana", "Pérez")));
        Assert.NotEmpty(Validar(new RegisterRequest("ana@example.com", null!, "Ana", "Pérez")));
        Assert.NotEmpty(Validar(new RegisterRequest("ana@example.com", "12345678", null!, "Pérez")));
        Assert.NotEmpty(Validar(new RegisterRequest("ana@example.com", "12345678", "Ana", null!)));
    }

    [Fact]
    public void Login_ValidaCorreoYContrasenaSinExigirMinimoDeRegistro()
    {
        Assert.Empty(Validar(new LoginRequest("ana@example.com", "x")));
        Assert.NotEmpty(Validar(new LoginRequest("correo-invalido", "x")));
        Assert.NotEmpty(Validar(new LoginRequest("ana@example.com", "")));
        Assert.NotEmpty(Validar(new LoginRequest("ana@example.com", new string('x', 129))));
    }

    private static UpdateProfileRequest Actualizar(
        string? nombre = null, string? apellido = null, string? telefono = null,
        string? preferencias = null, string? reglas = null) =>
        new(nombre, apellido, telefono, preferencias, reglas);

    [Theory]
    [InlineData("1123456789")]
    [InlineData("+54 9 11 2345-6789")]
    [InlineData("(011) 4567-8901")]
    [InlineData("+54 (11) 2345-6789")]
    [InlineData("12345678")]
    [InlineData("123456789012345")]
    public void Actualizacion_AceptaTelefonosValidos(string telefono)
    {
        Assert.Empty(Validar(Actualizar(telefono: telefono)));
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData("1234567890123456")]
    [InlineData("++++---")]
    [InlineData("+54 (11 12345678")]
    [InlineData("1234)5678(")]
    [InlineData("1234+5678")]
    [InlineData("1234\n5678")]
    [InlineData("1234--5678")]
    [InlineData("1234abcd5678")]
    public void Actualizacion_RechazaTelefonosInvalidos(string telefono)
    {
        Assert.NotEmpty(Validar(Actualizar(telefono: telefono)));
    }

    [Fact]
    public void Actualizacion_AdmiteCamposOpcionalesYControlaLimites()
    {
        Assert.Empty(Validar(Actualizar()));
        Assert.Empty(Validar(Actualizar(nombre: "María Jose", apellido: "O'Connor")));
        Assert.NotEmpty(Validar(Actualizar(nombre: "\t")));
        Assert.NotEmpty(Validar(Actualizar(apellido: "")));
        Assert.NotEmpty(Validar(Actualizar(telefono: new string('1', 31))));
        Assert.NotEmpty(Validar(Actualizar(preferencias: new string('x', 5001))));
        Assert.NotEmpty(Validar(Actualizar(reglas: new string('x', 2001))));
    }
}
