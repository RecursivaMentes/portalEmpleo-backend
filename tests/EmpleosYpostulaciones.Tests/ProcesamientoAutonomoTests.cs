using Application.Postulaciones;
using Domain.Entities;
using Domain.Enums;
using Xunit;

namespace EmpleosYpostulaciones.Tests;

public sealed class ProcesamientoAutonomoTests
{
    [Theory]
    [InlineData("Automatica", true)]
    [InlineData("Asistida", true)]
    [InlineData("Desactivada", false)]
    [InlineData(null, false)]
    [InlineData("invalida", false)]
    public void SoloModoAutonomoPuedeIniciar(string? modo, bool esperado) =>
        Assert.Equal(esperado, ProcesamientoAutonomoService.Permitido(modo));

    [Fact]
    public void VentanaDiariaEsUtcYNoDependeDelRelojLocal()
    {
        var (inicio, fin) = ProcesamientoAutonomoService.DiaUtc(
            new DateTime(2026, 11, 12, 22, 30, 0, DateTimeKind.Utc));
        Assert.Equal(new DateTime(2026, 11, 12), inicio);
        Assert.Equal(inicio.AddDays(1), fin);
    }

    [Theory]
    [InlineData("nocturno", true)]
    [InlineData("Remoto", true)]
    [InlineData("contrato_inexistente", false)]
    public void ExclusionesSeRespetan(string exclusion, bool excluida)
    {
        var oferta = new Empleo
        {
            Titulo = "Desarrollador", Descripcion = "Turno nocturno",
            Modalidad = ModalidadTrabajo.Remoto, TipoContrato = TipoContrato.TiempoCompleto
        };
        Assert.Equal(excluida, ProcesamientoAutonomoService.Excluida(oferta, [exclusion]));
    }

    [Fact]
    public void PendientesPrimeroYRecomendacionesPorCompatibilidadSinDescartadas()
    {
        var usuario = Guid.NewGuid();
        var pendiente = Guid.NewGuid();
        var vencida = Guid.NewGuid();
        var alta = Guid.NewGuid();
        var media = Guid.NewGuid();
        var descartada = Guid.NewGuid();
        var baja = Guid.NewGuid();
        var ahora = DateTime.UtcNow;
        var guardadas = new List<OfertaPendienteDatos>
        {
            new(usuario, pendiente, ahora, "Pendiente", ahora.AddDays(1), null,
                ModalidadTrabajo.Remoto, TipoContrato.TiempoCompleto, null),
            new(usuario, vencida, ahora, "Vencida", ahora.AddDays(-1), null,
                ModalidadTrabajo.Remoto, TipoContrato.TiempoCompleto, null)
        };
        var datos = new DatosExternosOrdenamiento("Prioridad", null, new Dictionary<Guid, decimal>
        {
            [pendiente] = 70, [vencida] = 99, [alta] = 95, [media] = 65,
            [descartada] = 100, [baja] = 49
        });
        var candidatos = OrdenamientoPendientesService.SeleccionarCandidatos(guardadas, datos,
            new HashSet<Guid> { descartada }, 50, ahora);
        Assert.Equal(new[] { pendiente, alta, media }, candidatos.Select(x => x.EmpleoId));
        Assert.Equal(PreparacionPostulacion.FuentePendientes, candidatos[0].FuenteOferta);
        Assert.All(candidatos.Skip(1), c => Assert.Equal(PreparacionPostulacion.FuenteRecomendaciones, c.FuenteOferta));
        Assert.Equal(alta, OrdenamientoPendientesService.SeleccionarCandidatos([], datos,
            new HashSet<Guid> { descartada, vencida }, 50, ahora)[0].EmpleoId);
    }

    [Fact]
    public void RecomendacionesRespetanPreferenciasYExclusiones()
    {
        var ahora = DateTime.UtcNow;
        var oferta = new Empleo { Estado = EstadoEmpleo.Publicado, Titulo = "Desarrollador Backend",
            Ubicacion = "Buenos Aires", Modalidad = ModalidadTrabajo.Remoto,
            TipoContrato = TipoContrato.TiempoCompleto, SalarioMin = 1000 };
        var preferencias = new PreferenciasProcesamiento(["Backend"], ["Buenos Aires"],
            ["Remoto"], ["TiempoCompleto"], 900, ["nocturno"]);
        Assert.True(ProcesamientoAutonomoService.Elegible(oferta, preferencias,
            PreparacionPostulacion.FuenteRecomendaciones, ahora));
        oferta.SalarioMin = null;
        Assert.False(ProcesamientoAutonomoService.Elegible(oferta, preferencias,
            PreparacionPostulacion.FuenteRecomendaciones, ahora));
        Assert.True(ProcesamientoAutonomoService.Elegible(oferta, preferencias,
            PreparacionPostulacion.FuentePendientes, ahora));
        oferta.Descripcion = "Turno nocturno";
        Assert.False(ProcesamientoAutonomoService.Elegible(oferta, preferencias,
            PreparacionPostulacion.FuentePendientes, ahora));
        oferta.Descripcion = "";
        oferta.FechaCierre = ahora;
        Assert.False(ProcesamientoAutonomoService.Elegible(oferta, null,
            PreparacionPostulacion.FuenteRecomendaciones, ahora));
    }

    [Fact]
    public void SinExclusionesNoBloquea()
    {
        Assert.False(ProcesamientoAutonomoService.Excluida(new Empleo { Titulo = "Ejemplo" }, null));
    }
}
