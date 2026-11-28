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
    public void RecomendacionesNoFiltranPreferenciasPeroRespetanExclusiones()
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
        oferta.Ubicacion = "Otra ubicación";
        oferta.Titulo = "Otro puesto";
        Assert.True(ProcesamientoAutonomoService.Elegible(oferta, preferencias,
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
    public void PendientesSinMinimoYRecomendacionesConPisoDe50()
    {
        var ahora = DateTime.UtcNow;
        var usuario = Guid.NewGuid();
        var baja = Guid.NewGuid(); var sinPuntaje = Guid.NewGuid();
        var recomendada = Guid.NewGuid(); var inferior = Guid.NewGuid();
        var guardadas = new List<OfertaPendienteDatos>
        {
            new(usuario, baja, ahora.AddDays(-2), "Baja", null, null,
                ModalidadTrabajo.Remoto, TipoContrato.TiempoCompleto, null),
            new(usuario, sinPuntaje, ahora.AddDays(-1), "Sin puntaje", null, null,
                ModalidadTrabajo.Remoto, TipoContrato.TiempoCompleto, null)
        };
        var datos = new DatosExternosOrdenamiento("Antiguos", null, new Dictionary<Guid, decimal>
        { [baja] = 0, [recomendada] = 50, [inferior] = 49 });
        var candidatos = OrdenamientoPendientesService.SeleccionarCandidatos(guardadas, datos,
            new HashSet<Guid>(), 0, ahora);
        Assert.Equal(new[] { baja, sinPuntaje, recomendada }, candidatos.Select(x => x.EmpleoId));
        Assert.Equal(new[] { baja, sinPuntaje }, OrdenamientoPendientesService.SeleccionarCandidatos(
            guardadas, datos, new HashSet<Guid>(), 90, ahora).Select(x => x.EmpleoId));
    }

    [Fact]
    public async Task SinPendientes_ConsultaRecomendacionesYRespetaSuOrden()
    {
        var alta = Guid.NewGuid();
        var igualAlMinimo = Guid.NewGuid();
        var inferior = Guid.NewGuid();
        var datos = new DatosExternosOrdenamiento("Prioridad", null, new Dictionary<Guid, decimal>
        { [inferior] = 49, [igualAlMinimo] = 50, [alta] = 95 });
        var servicio = new OrdenamientoPendientesService(new SinPendientesRepo(),
            new RecomendacionesInternasFalsas(datos), new RecomendacionesInternasFalsas(datos));
        var candidatos = await servicio.ListarCandidatosInternosAsync(Guid.NewGuid(), 50);
        Assert.Equal(new[] { alta, igualAlMinimo }, candidatos.Select(x => x.EmpleoId));
        Assert.All(candidatos, x => Assert.Equal(PreparacionPostulacion.FuenteRecomendaciones, x.FuenteOferta));
    }

    private sealed class SinPendientesRepo : IOrdenamientoPendientesRepository
    {
        public Task<List<OfertaPendienteDatos>> ObtenerPendientesAsync(Guid usuarioId, CancellationToken ct = default) =>
            Task.FromResult(new List<OfertaPendienteDatos>());
    }

    private sealed class RecomendacionesInternasFalsas(DatosExternosOrdenamiento datos) :
        IDatosExternosOrdenamiento, IDatosExternosOrdenamientoInterno
    {
        public Task<DatosExternosOrdenamiento> ObtenerAsync(Guid usuarioId, string accessToken, CancellationToken ct = default) => Task.FromResult(datos);
        public Task<DatosExternosOrdenamiento> ObtenerInternoAsync(Guid usuarioId, CancellationToken ct = default) => Task.FromResult(datos);
    }

    [Fact]
    public void SinExclusionesNoBloquea()
    {
        Assert.False(ProcesamientoAutonomoService.Excluida(new Empleo { Titulo = "Ejemplo" }, null));
    }
}
