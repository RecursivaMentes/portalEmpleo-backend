using Application.Herramientas;
using Xunit;

namespace Chatbot.Tests;

public class ConsultasOfertasIA_Tests
{
    private sealed class ConsultasSimuladas : IConsultaEmpleosChat
    {
        public string? UltimoFiltro { get; private set; }
        public Guid? UltimoEmpleo { get; private set; }
        public Task<string> ConsultarAsync(string filtro, Guid? empleoId, CancellationToken ct = default)
        {
            UltimoFiltro = filtro; UltimoEmpleo = empleoId;
            return Task.FromResult("""[{"id":"11111111-1111-1111-1111-111111111111","titulo":"Desarrollador","requisitos":"C#"}]""");
        }
    }

    [Fact]
    public void HerramientasSoloConsultan()
    {
        var h = new HerramientasEmpleos(new ConsultasSimuladas());
        Assert.Equal(2, h.Definiciones.Count);
        Assert.DoesNotContain(h.Definiciones, d => d.Nombre is "solicitar_postulacion_ia" or "crear_empleo" or "eliminar_empleo");
    }

    [Fact]
    public async Task BuscarTransmiteElFiltroAlContratoDeConsulta()
    {
        var consulta = new ConsultasSimuladas();
        var h = new HerramientasEmpleos(consulta);
        var resultado = await h.EjecutarAsync(new LlamadaHerramienta("buscar_empleos", "{\"texto\":\"backend\"}"), default);
        Assert.False(resultado.EsError);
        Assert.Equal("backend", consulta.UltimoFiltro);
        Assert.Null(consulta.UltimoEmpleo);
    }

    [Fact]
    public async Task NoExisteHerramientaDeEscritura()
    {
        var consulta = new ConsultasSimuladas();
        var h = new HerramientasEmpleos(consulta);
        var r = await h.EjecutarAsync(new LlamadaHerramienta("modificar_empleo", "{}"), default);
        Assert.True(r.EsError);
        Assert.Null(consulta.UltimoFiltro);
    }
}