using System.Net;
using System.Text;
using Recomendaciones.Infrastructure.Clients;
using Recomendaciones.Infrastructure.Services;
using Recomendaciones.Application.Services;
using Xunit;

namespace Recomendaciones.Tests;

public sealed class CompatibilidadInternaTests
{
    private sealed class Simulador(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken ct) => Task.FromResult(responder(request));
    }

    [Fact]
    public async Task AnalisisInternoUsaCredencialDeServicioYPreservaAlgoritmo()
    {
        var usuario = Guid.NewGuid();
        var empleo = Guid.NewGuid();
        var httpUsuarios = new HttpClient(new Simulador(req =>
        {
            Assert.Equal($"/internal/usuarios/{usuario}/perfil-laboral", req.RequestUri!.AbsolutePath);
            Assert.Equal("Recomendaciones", req.Headers.GetValues("X-Internal-Service").Single());
            Assert.Equal("clave-prueba", req.Headers.GetValues("X-Internal-Key").Single());
            Assert.Null(req.Headers.Authorization);
            return Json("""{"habilidades":[{"nombre":"C#"},{"nombre":"SQL"}]}""");
        })) { BaseAddress = new Uri("http://usuarios.test") };
        var httpEmpleos = new HttpClient(new Simulador(req =>
        {
            Assert.Equal("/api/empleos", req.RequestUri!.AbsolutePath);
            return Json($$"""[{"id":"{{empleo}}","titulo":"Backend","requisitos":"C#,SQL,Docker"}]""");
        })) { BaseAddress = new Uri("http://empleos.test") };
        var servicio = new AnalisisOfertasService(new UsuariosClient(httpUsuarios),
            new EmpleosClient(httpEmpleos), new CompatibilidadService());
        var resultado = await servicio.AnalizarInternoAsync(usuario, "clave-prueba");
        Assert.NotNull(resultado);
        var oferta = Assert.Single(resultado);
        Assert.Equal(empleo, oferta.EmpleoId);
        Assert.Equal(66.67m, oferta.Score);
        Assert.Contains("Docker", oferta.RequisitosFaltantes);
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
