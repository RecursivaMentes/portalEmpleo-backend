using Application.Postulaciones;
using Domain.Entities;
using Domain.Enums;
using Xunit;

namespace EmpleosYpostulaciones.Tests;

public class PostulacionServiceTests
{
    [Fact]
    public async Task SolicitudValida_CreaPostulacion()
    {
        var repositorio = new RepositorioFalso();
        var servicio = CrearServicio(repositorio);

        var postulacion = await servicio.CrearAsync(Solicitud(), "token");

        Assert.Equal(1, repositorio.Guardadas);
        Assert.Equal(EstadoPostulacion.Pendiente, postulacion.Estado);
    }

    [Fact]
    public async Task PresentacionOpcional_PermiteNull()
    {
        var repositorio = new RepositorioFalso();
        var servicio = CrearServicio(repositorio);

        await servicio.CrearAsync(Solicitud(texto: null), "token");

        Assert.Equal(1, repositorio.Guardadas);
    }

    [Fact]
    public async Task IdentificadorVacio_RechazaSinGuardar()
    {
        var repositorio = new RepositorioFalso();
        var solicitud = Solicitud() with { UsuarioId = Guid.Empty };

        var error = await Assert.ThrowsAsync<PostulacionException>(
            () => CrearServicio(repositorio).CrearAsync(solicitud, "token"));

        Assert.Equal("DATOS_INVALIDOS", error.CodigoError);
        Assert.Equal(0, repositorio.Guardadas);
    }

    [Fact]
    public async Task PresentacionDemasiadoLarga_RechazaSinGuardar()
    {
        var repositorio = new RepositorioFalso();

        var error = await Assert.ThrowsAsync<PostulacionException>(
            () => CrearServicio(repositorio).CrearAsync(Solicitud(texto: new string('x', 4001)), "token"));

        Assert.Equal("PRESENTACION_INVALIDA", error.CodigoError);
        Assert.Equal(0, repositorio.Guardadas);
    }

    [Fact]
    public async Task OfertaInexistente_DevuelveErrorEspecifico()
    {
        var repositorio = new RepositorioFalso { Oferta = null };

        var error = await Assert.ThrowsAsync<PostulacionException>(
            () => CrearServicio(repositorio).CrearAsync(Solicitud(), "token"));

        Assert.Equal("OFERTA_NO_ENCONTRADA", error.CodigoError);
    }

    [Fact]
    public async Task OfertaNoPublicada_DevuelveErrorEspecifico()
    {
        var repositorio = new RepositorioFalso();
        repositorio.Oferta!.Estado = EstadoEmpleo.Borrador;

        var error = await Assert.ThrowsAsync<PostulacionException>(
            () => CrearServicio(repositorio).CrearAsync(Solicitud(), "token"));

        Assert.Equal("OFERTA_NO_DISPONIBLE", error.CodigoError);
    }

    [Fact]
    public async Task OfertaVencida_DevuelveErrorEspecifico()
    {
        var repositorio = new RepositorioFalso();
        repositorio.Oferta!.FechaCierre = DateTime.UtcNow.AddDays(-1);

        var error = await Assert.ThrowsAsync<PostulacionException>(
            () => CrearServicio(repositorio).CrearAsync(Solicitud(), "token"));

        Assert.Equal("OFERTA_FINALIZADA", error.CodigoError);
    }

    [Fact]
    public async Task PerfilIncompleto_InformaFaltantes()
    {
        var repositorio = new RepositorioFalso();
        var perfil = new PerfilFalso
        {
            Resultado = new ValidacionPerfilLaboralResultado(false, ["Resumen profesional", "Habilidades"])
        };

        var error = await Assert.ThrowsAsync<PostulacionException>(
            () => CrearServicio(repositorio, perfil: perfil).CrearAsync(Solicitud(), "token"));

        Assert.Equal("PERFIL_INCOMPLETO", error.CodigoError);
        Assert.Contains("Habilidades", error.Message);
        Assert.Equal(0, repositorio.Guardadas);
    }

    [Fact]
    public async Task CvAjeno_RechazaPostulacion()
    {
        var repositorio = new RepositorioFalso();

        var error = await Assert.ThrowsAsync<PostulacionException>(
            () => CrearServicio(repositorio, cv: new CvFalso { Propio = false })
                .CrearAsync(Solicitud(), "token"));

        Assert.Equal("CV_NO_ENCONTRADO", error.CodigoError);
        Assert.Equal(0, repositorio.Guardadas);
    }

    [Fact]
    public async Task Duplicado_RechazaPostulacion()
    {
        var repositorio = new RepositorioFalso { YaPostulado = true };

        var error = await Assert.ThrowsAsync<PostulacionException>(
            () => CrearServicio(repositorio).CrearAsync(Solicitud(), "token"));

        Assert.Equal("POSTULACION_DUPLICADA", error.CodigoError);
        Assert.Equal(0, repositorio.Guardadas);
    }

    [Fact]
    public async Task FallaDeUsuarios_NoRegistraPostulacion()
    {
        var repositorio = new RepositorioFalso();
        var perfil = new PerfilFalso { Error = new HttpRequestException("Usuarios no disponible") };

        await Assert.ThrowsAsync<HttpRequestException>(
            () => CrearServicio(repositorio, perfil: perfil).CrearAsync(Solicitud(), "token"));

        Assert.Equal(0, repositorio.Guardadas);
    }

    private static PostulacionService CrearServicio(
        RepositorioFalso repositorio, CvFalso? cv = null, PerfilFalso? perfil = null) =>
        new(repositorio, cv ?? new CvFalso(), perfil ?? new PerfilFalso());

    private static CrearPostulacionRequest Solicitud(string? texto = "Presentación") =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), texto);

    private sealed class RepositorioFalso : IPostulacionRepository
    {
        public Empleo? Oferta { get; set; } = new()
        {
            Id = Guid.NewGuid(),
            Estado = EstadoEmpleo.Publicado,
            FechaCierre = DateTime.UtcNow.AddDays(10)
        };
        public bool YaPostulado { get; set; }
        public int Guardadas { get; private set; }

        public Task<Empleo?> ObtenerEmpleoAsync(Guid empleoId, CancellationToken ct = default) =>
            Task.FromResult(Oferta);

        public Task<bool> ExistePostulacionAsync(Guid usuarioId, Guid empleoId, CancellationToken ct = default) =>
            Task.FromResult(YaPostulado);

        public Task AgregarAsync(Postulacion postulacion, CancellationToken ct = default)
        {
            Guardadas++;
            return Task.CompletedTask;
        }

        public Task<List<Postulacion>> ObtenerPorUsuarioAsync(Guid usuarioId, CancellationToken ct = default) =>
            Task.FromResult(new List<Postulacion>());

        public Task<Postulacion?> ObtenerPorIdAsync(Guid postulacionId, CancellationToken ct = default) =>
            Task.FromResult<Postulacion?>(null);
    }

    private sealed class CvFalso : ICvValidationClient
    {
        public bool Propio { get; set; } = true;

        public Task<bool> PerteneceAlUsuarioAsync(Guid cvId, string token, CancellationToken ct = default) =>
            Task.FromResult(Propio);
    }

    private sealed class PerfilFalso : IPerfilLaboralValidationClient
    {
        public ValidacionPerfilLaboralResultado Resultado { get; set; } = new(true, []);
        public Exception? Error { get; set; }

        public Task<ValidacionPerfilLaboralResultado> ValidarAsync(string token, CancellationToken ct = default) =>
            Error is not null
                ? Task.FromException<ValidacionPerfilLaboralResultado>(Error)
                : Task.FromResult(Resultado);
    }
}
