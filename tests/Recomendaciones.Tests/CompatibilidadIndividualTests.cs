using System.Net;
using System.Text;
using Recomendaciones.Application.Services;
using Recomendaciones.Infrastructure.Clients;
using Recomendaciones.Infrastructure.Exceptions;
using Recomendaciones.Infrastructure.Services;
using Xunit;

namespace Recomendaciones.Tests;

public sealed class CompatibilidadIndividualTests
{
    private static readonly Guid UsuarioId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid EmpleoId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtroEmpleoId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private const string Token = "jwt-prueba";
    private const string Perfil = """
        {
          "habilidades": [{"nombre":"C#"},{"nombre":"SQL"},{"nombre":"React"}],
          "resumen": "Desarrollador"
        }
        """;

    private static string Empleos => $$"""
        [
          {"id":"{{OtroEmpleoId}}","titulo":"Oferta Python","requisitos":"Python,Django"},
          {"id":"{{EmpleoId}}","titulo":"Oferta C#","requisitos":"C#,SQL,Docker"}
        ]
        """;

    [Fact]
    public async Task AnalizaSoloLaOfertaSolicitada_ConElAlgoritmoExistente()
    {
        var servicio = CrearServicio(Perfil, Empleos);
        var (resultado, error) = await servicio.AnalizarEmpleoAsync(UsuarioId, EmpleoId, Token);

        Assert.Null(error);
        Assert.NotNull(resultado);
        Assert.Equal(UsuarioId, resultado.UsuarioId);
        Assert.Equal(EmpleoId, resultado.EmpleoId);
        Assert.Equal("Oferta C#", resultado.Titulo);
        Assert.Equal(66.67m, resultado.Score);
        Assert.Contains("C#", resultado.Coincidencias);
        Assert.Contains("SQL", resultado.Coincidencias);
        Assert.Equal(new[] { "Docker" }, resultado.RequisitosFaltantes);
    }

    [Fact]
    public async Task OfertaNoPublicadaEnElListado_DevuelveNoEncontrada()
    {
        var (resultado, error) = await CrearServicio(Perfil, "[]")
            .AnalizarEmpleoAsync(UsuarioId, EmpleoId, Token);
        Assert.Null(resultado);
        Assert.Equal("EMPLEO_NO_ENCONTRADO", error);
    }

    [Fact]
    public async Task PerfilNoEncontrado_DevuelveErrorEspecifico()
    {
        var (resultado, error) = await CrearServicio("{}", Empleos, HttpStatusCode.NotFound)
            .AnalizarEmpleoAsync(UsuarioId, EmpleoId, Token);
        Assert.Null(resultado);
        Assert.Equal("PERFIL_NO_ENCONTRADO", error);
    }

    [Fact]
    public async Task OfertaSinRequisitos_NoDevuelveCompatibilidadFicticia()
    {
        var empleos = $$""" [{"id":"{{EmpleoId}}","titulo":"Sin requisitos","requisitos":" "}] """;
        var (resultado, error) = await CrearServicio(Perfil, empleos)
            .AnalizarEmpleoAsync(UsuarioId, EmpleoId, Token);
        Assert.Null(resultado);
        Assert.Equal("REQUISITOS_INSUFICIENTES", error);
    }

    [Fact]
    public async Task ServicioUsuariosNoDisponible_PropagaFallaSinCalcular()
    {
        var servicio = CrearServicio("{}", Empleos, HttpStatusCode.ServiceUnavailable);
        await Assert.ThrowsAsync<ServicioExternoException>(
            () => servicio.AnalizarEmpleoAsync(UsuarioId, EmpleoId, Token));
    }

    private static AnalisisOfertasService CrearServicio(
        string perfil, string empleos, HttpStatusCode estadoPerfil = HttpStatusCode.OK)
    {
        var usuariosHttp = new HttpClient(new Handler(request =>
        {
            Assert.Equal("/api/perfil-laboral/me", request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal(Token, request.Headers.Authorization?.Parameter);
            return Json(perfil, estadoPerfil);
        })) { BaseAddress = new Uri("http://usuarios.test") };

        var empleosHttp = new HttpClient(new Handler(request =>
        {
            Assert.Equal("/api/empleos", request.RequestUri!.AbsolutePath);
            return Json(empleos);
        })) { BaseAddress = new Uri("http://empleos.test") };

        return new AnalisisOfertasService(
            new UsuariosClient(usuariosHttp),
            new EmpleosClient(empleosHttp),
            new CompatibilidadService());
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
