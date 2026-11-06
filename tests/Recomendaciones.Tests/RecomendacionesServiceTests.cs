using System.Net;
using System.Text;
using Recomendaciones.Application.Services;
using Recomendaciones.Infrastructure.Clients;
using Recomendaciones.Infrastructure.Services;
using Xunit;

namespace Recomendaciones.Tests;

public class RecomendacionesServiceTests
{
    private static readonly Guid UsuarioId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid EmpleoDotNetId = Guid.NewGuid();
    private static readonly Guid EmpleoReactId = Guid.NewGuid();
    private static readonly Guid EmpleoLinuxId = Guid.NewGuid();

    private sealed class HttpHandlerSimulado : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public HttpHandlerSimulado(
            Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }

    private static HttpResponseMessage RespuestaJson(
        string json,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json")
        };
    }

    private static RecomendacionesService CrearServicio(
        string perfilJson,
        string empleosJson,
        HttpStatusCode perfilStatus = HttpStatusCode.OK)
    {
        var usuariosHttp = new HttpClient(
            new HttpHandlerSimulado(request =>
            {
                Assert.Equal(
                    $"/api/perfiles/{UsuarioId}",
                    request.RequestUri!.AbsolutePath);

                return RespuestaJson(perfilJson, perfilStatus);
            }))
        {
            BaseAddress = new Uri("http://usuarios.test")
        };

        var empleosHttp = new HttpClient(
            new HttpHandlerSimulado(request =>
            {
                Assert.Equal(
                    "/api/empleos/activos",
                    request.RequestUri!.AbsolutePath);

                return RespuestaJson(empleosJson);
            }))
        {
            BaseAddress = new Uri("http://empleos.test")
        };

        var analisisService = new AnalisisOfertasService(
            new UsuariosClient(usuariosHttp),
            new EmpleosClient(empleosHttp),
            new CompatibilidadService()
        );

        return new RecomendacionesService(analisisService);
    }

    private const string PerfilValido = """
        {
          "usuarioId": "11111111-1111-1111-1111-111111111111",
          "habilidadesJson": "[\"C#\",\"SQL\",\"React\"]",
          "resumen": "Desarrollador de software"
        }
        """;

    private static string EmpleosValidos() => $$"""
        [
          {
            "id": "{{EmpleoReactId}}",
            "titulo": "Desarrollador React",
            "requisitos": "React,JavaScript",
            "modalidad": 1,
            "estado": 1,
            "fechaCierre": null
          },
          {
            "id": "{{EmpleoDotNetId}}",
            "titulo": "Desarrollador .NET",
            "requisitos": "C#,SQL,Docker",
            "modalidad": 1,
            "estado": 1,
            "fechaCierre": null
          },
          {
            "id": "{{EmpleoLinuxId}}",
            "titulo": "Administrador Linux",
            "requisitos": "Linux,Docker,Kubernetes",
            "modalidad": 0,
            "estado": 1,
            "fechaCierre": null
          }
        ]
        """;

    [Fact]
    public async Task Recomendaciones_SeOrdenanPorCompatibilidad()
    {
        var servicio = CrearServicio(
            PerfilValido,
            EmpleosValidos());

        var resultado =
            await servicio.ObtenerRecomendacionesAsync(UsuarioId);

        Assert.NotNull(resultado);
        Assert.Equal(3, resultado.Count);

        Assert.Equal("Desarrollador .NET", resultado[0].Titulo);
        Assert.Equal(66.67m, resultado[0].PorcentajeCompatibilidad);

        Assert.Equal("Desarrollador React", resultado[1].Titulo);
        Assert.Equal(50m, resultado[1].PorcentajeCompatibilidad);

        Assert.Equal("Administrador Linux", resultado[2].Titulo);
        Assert.Equal(0m, resultado[2].PorcentajeCompatibilidad);
    }

    [Fact]
    public async Task Recomendaciones_DevuelvenHabilidadesCoincidentesYFaltantes()
    {
        var servicio = CrearServicio(
            PerfilValido,
            EmpleosValidos());

        var resultado =
            await servicio.ObtenerRecomendacionesAsync(UsuarioId);

        Assert.NotNull(resultado);

        var empleoDotNet = resultado.Single(
            r => r.EmpleoId == EmpleoDotNetId);

        Assert.Contains("C#", empleoDotNet.Coincidencias);
        Assert.Contains("SQL", empleoDotNet.Coincidencias);
        Assert.Contains("Docker", empleoDotNet.RequisitosFaltantes);

        Assert.Equal(
            "El usuario cumple 2 de 3 requisitos.",
            empleoDotNet.Motivo);
    }

    [Fact]
    public async Task UsuarioSinPerfil_RetornaNull()
    {
        var servicio = CrearServicio(
            "{}",
            "[]",
            HttpStatusCode.NotFound);

        var resultado =
            await servicio.ObtenerRecomendacionesAsync(UsuarioId);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task SinEmpleos_RetornaListaVacia()
    {
        var servicio = CrearServicio(
            PerfilValido,
            "[]");

        var resultado =
            await servicio.ObtenerRecomendacionesAsync(UsuarioId);

        Assert.NotNull(resultado);
        Assert.Empty(resultado);
    }

    [Fact]
    public async Task EmpleosCerradosYVencidos_NoSeRecomiendan()
    {
        var empleos = $$"""
        [
          {
            "id": "{{Guid.NewGuid()}}",
            "titulo": "Oferta cerrada",
            "requisitos": "C#,SQL",
            "modalidad": 1,
            "estado": 2,
            "fechaCierre": null
          },
          {
            "id": "{{Guid.NewGuid()}}",
            "titulo": "Oferta vencida",
            "requisitos": "React",
            "modalidad": 1,
            "estado": 1,
            "fechaCierre": "2020-01-01T00:00:00Z"
          },
          {
            "id": "{{EmpleoDotNetId}}",
            "titulo": "Oferta activa",
            "requisitos": "C#,SQL",
            "modalidad": 1,
            "estado": 1,
            "fechaCierre": null
          }
        ]
        """;

        var servicio = CrearServicio(
            PerfilValido,
            empleos);

        var resultado =
            await servicio.ObtenerRecomendacionesAsync(UsuarioId);

        Assert.NotNull(resultado);
        Assert.Single(resultado);
        Assert.Equal("Oferta activa", resultado[0].Titulo);
        Assert.Equal(100m, resultado[0].PorcentajeCompatibilidad);
    }

    [Fact]
    public async Task EmpleosSinRequisitos_NoSeRecomiendan()
    {
        var empleos = $$"""
        [
          {
            "id": "{{Guid.NewGuid()}}",
            "titulo": "Oferta sin requisitos",
            "requisitos": "",
            "modalidad": 1,
            "estado": 1,
            "fechaCierre": null
          }
        ]
        """;

        var servicio = CrearServicio(
            PerfilValido,
            empleos);

        var resultado =
            await servicio.ObtenerRecomendacionesAsync(UsuarioId);

        Assert.NotNull(resultado);
        Assert.Empty(resultado);
    }
}
