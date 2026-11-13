using Application.Herramientas;
using Xunit;

namespace Chatbot.Tests;

public sealed class SolicitudesPostulacionIA_Tests
{
    private sealed class Captura : IPostulacionesUsuarioClient
    {
        public string? Token { get; private set; }
        public Guid EmpleoId { get; private set; }
        public Task<ResultadoSolicitudPostulacionIA> SolicitarAsync(string accessToken,
            Guid empleoId, CancellationToken ct = default)
        {
            Token = accessToken; EmpleoId = empleoId;
            return Task.FromResult(new ResultadoSolicitudPostulacionIA(true, "PendienteAprobacion", null));
        }
    }
    [Fact]
    public async Task SolicitudExpresa_UsaIdentidadAutenticadaYNoEditaEmpleo()
    {
        var captura = new Captura();
        var tool = new HerramientasPostulaciones(captura, "jwt-de-prueba");
        var respuesta = await tool.EjecutarAsync(new LlamadaHerramienta("solicitar_postulacion_ia",
            "{\"empleo_id\":\"11111111-1111-1111-1111-111111111111\"}"), default);
        Assert.False(respuesta.EsError);
        Assert.Equal("jwt-de-prueba", captura.Token);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), captura.EmpleoId);
        Assert.Single(tool.Definiciones);
        Assert.DoesNotContain(tool.Definiciones, x => x.Nombre.Contains("modificar"));
    }
}