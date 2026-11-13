using System.Net;
using System.Text;
using Application.Herramientas;
using Xunit;

namespace Chatbot.Tests;

public class ConsultasOfertasIA_Tests
{
    private sealed class Simulador : HttpMessageHandler
    {
        public HttpMethod? UltimoMetodo { get; private set; }
        public string? UltimaRuta { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            UltimoMetodo = request.Method;
            UltimaRuta = request.RequestUri?.PathAndQuery;
            var json = """[{"id":"11111111-1111-1111-1111-111111111111","titulo":"Desarrollador","requisitos":"C#"}]""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    [Fact]
    public void LasHerramientasSoloPermitenConsultar()
    {
        var h = new HerramientasEmpleos(new HttpClient { BaseAddress = new Uri("http://localhost/") });
        Assert.Equal(2, h.Definiciones.Count);
        Assert.All(h.Definiciones, d => Assert.DoesNotContain("modificar", d.Nombre));
        Assert.DoesNotContain(h.Definiciones, d => d.Nombre is "crear_empleo" or "eliminar_empleo");
    }

    [Fact]
    public async Task BuscarSoloRealizaGet()
    {
        var fake = new Simulador();
        var h = new HerramientasEmpleos(new HttpClient(fake) { BaseAddress = new Uri("http://localhost/") });
        var resultado = await h.EjecutarAsync(new LlamadaHerramienta("buscar_empleos", "{\"texto\":\"backend\"}"), default);
        Assert.False(resultado.EsError);
        Assert.Equal(HttpMethod.Get, fake.UltimoMetodo);
        Assert.Equal("/api/empleos?busqueda=backend", fake.UltimaRuta);
    }

    [Fact]
    public async Task NoExisteHerramientaDeEscritura()
    {
        var fake = new Simulador();
        var h = new HerramientasEmpleos(new HttpClient(fake) { BaseAddress = new Uri("http://localhost/") });
        var r = await h.EjecutarAsync(new LlamadaHerramienta("modificar_empleo", "{}"), default);
        Assert.True(r.EsError);
        Assert.Null(fake.UltimoMetodo);
    }
}