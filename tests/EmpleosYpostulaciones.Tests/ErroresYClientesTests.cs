using System.Net;
using System.Text.Json;
using Application.Postulaciones;
using EmpleosYpostulaciones.Api.Exceptions;
using EmpleosYpostulaciones.Infrastructure.Clients;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EmpleosYpostulaciones.Tests;

public class ErroresYClientesTests
{
    [Theory]
    [InlineData("POSTULACION_DUPLICADA", 409)]
    [InlineData("PERFIL_INCOMPLETO", 409)]
    [InlineData("OFERTA_NO_ENCONTRADA", 404)]
    [InlineData("PRESENTACION_INVALIDA", 400)]
    public async Task Manejador_DevuelveEstadoYCodigoError(string codigo, int esperado)
    {
        var error = codigo switch
        {
            "POSTULACION_DUPLICADA" => PostulacionException.Duplicada(),
            "PERFIL_INCOMPLETO" => PostulacionException.PerfilIncompleto(["Resumen"]),
            "OFERTA_NO_ENCONTRADA" => PostulacionException.OfertaNoEncontrada(),
            _ => PostulacionException.PresentacionInvalida()
        };
        var (estado, cuerpo) = await EjecutarHandler(error);

        Assert.Equal(esperado, estado);
        Assert.Equal(codigo, cuerpo.GetProperty("codigoError").GetString());
        Assert.False(string.IsNullOrWhiteSpace(cuerpo.GetProperty("mensaje").GetString()));
    }

    [Fact]
    public async Task ExcepcionInterna_NoSeConfundeConConflictoDeNegocio()
    {
        var (estado, cuerpo) = await EjecutarHandler(new InvalidOperationException("interno"));
        Assert.Equal(500, estado);
        Assert.Equal("ERROR_INTERNO", cuerpo.GetProperty("codigoError").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, 401)]
    [InlineData(HttpStatusCode.Forbidden, 403)]
    public async Task ClienteCv_ConservaErrorAutorizacion(HttpStatusCode estado, int esperado)
    {
        using var http = new HttpClient(new RespuestaFalsa(estado))
        {
            BaseAddress = new Uri("http://localhost:5000/")
        };
        var cliente = new CvValidationHttpClient(http);

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => cliente.PerteneceAlUsuarioAsync(Guid.NewGuid(), "token"));
        Assert.Equal(esperado, (int)error.StatusCode!.Value);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, 401)]
    [InlineData(HttpStatusCode.Forbidden, 403)]
    public async Task ClientePerfil_ConservaErrorAutorizacion(HttpStatusCode estado, int esperado)
    {
        using var http = new HttpClient(new RespuestaFalsa(estado))
        {
            BaseAddress = new Uri("http://localhost:5000/")
        };
        var cliente = new PerfilLaboralValidationHttpClient(http);

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => cliente.ValidarAsync("token"));
        Assert.Equal(esperado, (int)error.StatusCode!.Value);
    }

    [Fact]
    public async Task ClientePerfil_ConRespuestaInvalida_NoApruebaPostulacion()
    {
        using var http = new HttpClient(new RespuestaFalsa(HttpStatusCode.OK, "{json-invalido"))
        {
            BaseAddress = new Uri("http://localhost:5000/")
        };
        var cliente = new PerfilLaboralValidationHttpClient(http);

        await Assert.ThrowsAsync<HttpRequestException>(() => cliente.ValidarAsync("token"));
    }

    private static async Task<(int Estado, JsonElement Cuerpo)> EjecutarHandler(Exception error)
    {
        var context = new DefaultHttpContext();
        using var memoria = new MemoryStream();
        context.Response.Body = memoria;
        var handler = new PostulacionesExceptionHandler(
            NullLogger<PostulacionesExceptionHandler>.Instance);

        await handler.TryHandleAsync(context, error, CancellationToken.None);
        memoria.Position = 0;
        using var documento = await JsonDocument.ParseAsync(memoria);
        return (context.Response.StatusCode, documento.RootElement.Clone());
    }

    private sealed class RespuestaFalsa(HttpStatusCode estado, string? contenido = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(estado)
            {
                Content = new StringContent(contenido ?? string.Empty)
            });
    }
}
