using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Application.Auth;
using Application.Postulaciones;
using Domain.Entities;
using Xunit;

namespace Usuarios.Tests;

public class ConfiguracionPostulacionesTests
{
    [Fact]
    public async Task SinConfiguracion_DevuelveValoresPredeterminadosSinModificarUsuario()
    {
        var repo = new RepositorioFalso();
        var config = await new ConfiguracionPostulacionesService(repo).ObtenerAsync(repo.Usuario.Id);

        Assert.NotNull(config);
        Assert.Equal("Desactivada", config.Modo);
        Assert.Equal(60, config.CompatibilidadMinima);
        Assert.Equal(5, config.LimiteDiario);
        Assert.Equal(0, repo.Actualizaciones);
    }

    [Fact]
    public async Task Guardar_PreservaOtrasPreferenciasYReglasExistentes()
    {
        var repo = new RepositorioFalso();
        repo.Usuario.PreferenciasJson = "{\"tema\":\"oscuro\",\"ubicacion\":\"Quilmes\"}";
        repo.Usuario.ReglasPostulacion = "Texto preexistente";
        var solicitud = Solicitud() with
        {
            Preferencias = PreferenciasLaborales.Vacias() with
            {
                PuestosInteres = ["  Programador  ", "programador", "Tester"],
                Exclusiones = ["Turno nocturno"],
                ExpectativaSalarial = 700000
            }
        };

        var resultado = await new ConfiguracionPostulacionesService(repo)
            .GuardarAsync(repo.Usuario.Id, solicitud);

        Assert.NotNull(resultado);
        Assert.Equal("Automatica", resultado.Modo);
        Assert.Equal(["Programador", "Tester"], resultado.Preferencias!.PuestosInteres);
        Assert.Equal(["Turno nocturno"], resultado.Preferencias.Exclusiones);
        Assert.Equal("Texto preexistente", repo.Usuario.ReglasPostulacion);
        Assert.Equal(1, repo.Actualizaciones);
        using var documento = JsonDocument.Parse(repo.Usuario.PreferenciasJson);
        Assert.Equal("oscuro", documento.RootElement.GetProperty("tema").GetString());
        Assert.Equal("Quilmes", documento.RootElement.GetProperty("ubicacion").GetString());
        Assert.True(documento.RootElement.TryGetProperty("postulacionesInteligentes", out _));
    }

    [Fact]
    public async Task GuardarYPedir_RecuperaLaConfiguracionPersistida()
    {
        var repo = new RepositorioFalso();
        var servicio = new ConfiguracionPostulacionesService(repo);
        await servicio.GuardarAsync(repo.Usuario.Id, Solicitud() with { Modo = "asistida" });
        var recuperada = await servicio.ObtenerAsync(repo.Usuario.Id);

        Assert.NotNull(recuperada);
        Assert.Equal("Asistida", recuperada.Modo);
        Assert.Equal(60, recuperada.CompatibilidadMinima);
        Assert.Equal(5, recuperada.LimiteDiario);
    }

    [Theory]
    [InlineData("Incorrecto")]
    [InlineData("")]
    [InlineData(null)]
    public async Task ModoInvalido_NoGuarda(string? modo)
    {
        var repo = new RepositorioFalso();
        await Assert.ThrowsAsync<ValidationException>(() =>
            new ConfiguracionPostulacionesService(repo).GuardarAsync(repo.Usuario.Id, Solicitud() with { Modo = modo }));
        Assert.Equal(0, repo.Actualizaciones);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task CompatibilidadFueraDeRango_NoGuarda(int minimo)
    {
        var repo = new RepositorioFalso();
        await Assert.ThrowsAsync<ValidationException>(() =>
            new ConfiguracionPostulacionesService(repo).GuardarAsync(
                repo.Usuario.Id, Solicitud() with { CompatibilidadMinima = minimo }));
        Assert.Equal(0, repo.Actualizaciones);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public async Task LimiteFueraDeRango_NoGuarda(int limite)
    {
        var repo = new RepositorioFalso();
        await Assert.ThrowsAsync<ValidationException>(() =>
            new ConfiguracionPostulacionesService(repo).GuardarAsync(
                repo.Usuario.Id, Solicitud() with { LimiteDiario = limite }));
        Assert.Equal(0, repo.Actualizaciones);
    }

    [Fact]
    public async Task ExpectativaSalarialNegativa_NoGuarda()
    {
        var repo = new RepositorioFalso();
        var solicitud = Solicitud() with
        {
            Preferencias = PreferenciasLaborales.Vacias() with { ExpectativaSalarial = -1 }
        };
        await Assert.ThrowsAsync<ValidationException>(() =>
            new ConfiguracionPostulacionesService(repo).GuardarAsync(repo.Usuario.Id, solicitud));
        Assert.Equal(0, repo.Actualizaciones);
    }

    [Fact]
    public async Task PreferenciasOmitidas_NoBorraLasGuardadas()
    {
        var repo = new RepositorioFalso();
        var servicio = new ConfiguracionPostulacionesService(repo);
        var preferencias = PreferenciasLaborales.Vacias() with { Exclusiones = ["Guardias"] };
        await servicio.GuardarAsync(repo.Usuario.Id, Solicitud() with { Preferencias = preferencias });
        var resultado = await servicio.GuardarAsync(repo.Usuario.Id,
            Solicitud() with { Modo = "Desactivada", Preferencias = null });

        Assert.NotNull(resultado);
        Assert.Equal("Desactivada", resultado.Modo);
        Assert.Equal(["Guardias"], resultado.Preferencias!.Exclusiones);
    }

    [Fact]
    public async Task CuentaInactiva_NoEntregaNiGuardaConfiguracion()
    {
        var repo = new RepositorioFalso();
        repo.Usuario.Estado = false;
        var servicio = new ConfiguracionPostulacionesService(repo);

        Assert.Null(await servicio.ObtenerAsync(repo.Usuario.Id));
        Assert.Null(await servicio.GuardarAsync(repo.Usuario.Id, Solicitud()));
        Assert.Equal(0, repo.Actualizaciones);
    }

    [Theory]
    [InlineData("Prioridad")]
    [InlineData("Compatibilidad")]
    [InlineData("FechaCierre")]
    [InlineData("Antiguos")]
    [InlineData("Recientes")]
    public async Task OrdenUnico_SeGuardaYRecupera(string orden)
    {
        var repo = new RepositorioFalso();
        var servicio = new ConfiguracionPostulacionesService(repo);
        await servicio.GuardarAsync(repo.Usuario.Id, Solicitud() with { OrdenPendientes = orden });
        var resultado = await servicio.ObtenerAsync(repo.Usuario.Id);
        Assert.Equal(orden, resultado!.OrdenPendientes);
    }

    [Fact]
    public async Task OrdenInvalido_NoSeGuarda()
    {
        var repo = new RepositorioFalso();
        await Assert.ThrowsAsync<ValidationException>(() =>
            new ConfiguracionPostulacionesService(repo).GuardarAsync(
                repo.Usuario.Id, Solicitud() with { OrdenPendientes = "Otro" }));
    }

    private static ConfiguracionPostulaciones Solicitud() =>
        new("Automatica", 60, 5, PreferenciasLaborales.Vacias());

    private sealed class RepositorioFalso : IUsuarioRepository
    {
        public Usuario Usuario { get; } = new()
        {
            Id = Guid.NewGuid(), Email = "test@example.com", Nombre = "Test",
            Apellido = "Usuario", Estado = true, PreferenciasJson = "{}"
        };
        public int Actualizaciones { get; private set; }

        public Task<Usuario?> GetByEmailAsync(string email, CancellationToken ct = default) =>
            Task.FromResult<Usuario?>(string.Equals(email, Usuario.Email, StringComparison.OrdinalIgnoreCase) ? Usuario : null);

        public Task<Usuario?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<Usuario?>(id == Usuario.Id ? Usuario : null);

        public Task AddAsync(Usuario usuario, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateAsync(Usuario usuario, CancellationToken ct = default)
        {
            Actualizaciones++;
            return Task.CompletedTask;
        }
    }
}
