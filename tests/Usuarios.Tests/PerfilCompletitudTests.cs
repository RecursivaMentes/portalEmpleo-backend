using Application.PerfilLaboral;
using Application.PerfilLaboral.Dtos;
using Application.PerfilLaboral.Responses;
using Domain.Entities;
using Xunit;

namespace Usuarios.Tests;

public class PerfilCompletitudTests
{
    [Fact]
    public void ConPerfilCompletoYSinExperienciaDeclarada_DevuelveCompleto()
    {
        var resultado = PerfilCompletitud.Evaluar(UsuarioValido(), PerfilValido());
        Assert.True(resultado.PerfilCompleto);
        Assert.Empty(resultado.Faltantes);
    }

    [Fact]
    public void ConExperienciaRegistrada_PuedeEstarCompleto()
    {
        var perfil = PerfilValido() with
        {
            Experiencias = [new ExperienciaLaboralDto("Empresa", "Programador", null, null,
                new DateOnly(2024, 1, 1), null, true)],
            SinExperiencia = false
        };
        Assert.True(PerfilCompletitud.Evaluar(UsuarioValido(), perfil).PerfilCompleto);
    }

    [Fact]
    public void SinPerfilLaboral_IndicaFaltantes()
    {
        var resultado = PerfilCompletitud.Evaluar(UsuarioValido(), null);
        Assert.False(resultado.PerfilCompleto);
        Assert.Contains("Resumen profesional", resultado.Faltantes);
        Assert.Contains("Habilidades", resultado.Faltantes);
        Assert.Contains("Experiencia laboral o declaraciÃ³n SinExperiencia", resultado.Faltantes);
    }

    [Fact]
    public void SinResumenONingunaHabilidad_IndicaAmbosFaltantes()
    {
        var perfil = PerfilValido() with { Resumen = " ", Habilidades = [] };
        var resultado = PerfilCompletitud.Evaluar(UsuarioValido(), perfil);
        Assert.False(resultado.PerfilCompleto);
        Assert.Contains("Resumen profesional", resultado.Faltantes);
        Assert.Contains("Habilidades", resultado.Faltantes);
    }

    [Fact]
    public void SinEmailValido_IndicaElCampo()
    {
        var usuario = UsuarioValido();
        usuario.Email = "correo-invalido";
        var resultado = PerfilCompletitud.Evaluar(usuario, PerfilValido());
        Assert.False(resultado.PerfilCompleto);
        Assert.Contains("Correo electrÃ³nico", resultado.Faltantes);
    }

    private static Usuario UsuarioValido() => new()
    {
        Id = Guid.NewGuid(), Nombre = "Lucia", Apellido = "Perez", Email = "lucia@example.com", Estado = true
    };

    private static PerfilLaboralResponse PerfilValido() => new(
        Guid.NewGuid(), "Resumen profesional", null, true, [],
        [new HabilidadDto("C#", "Intermedio")], [], DateTime.UtcNow);
}
