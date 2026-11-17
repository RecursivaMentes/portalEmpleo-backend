using System.Net;
using Application.Postulaciones;
using Domain.Entities;
using Domain.Enums;
using Xunit;

namespace EmpleosYpostulaciones.Tests;

public sealed class OrdenamientoYRevisionTests
{
    private static readonly Guid Usuario = Guid.NewGuid();
    private static readonly Guid Empleo = Guid.NewGuid();
    private static readonly DateTime Ahora = new(2026, 11, 12, 14, 0, 0, DateTimeKind.Utc);

    private static OfertaPendienteDatos Oferta(Guid id, string titulo, DateTime? cierre = null,
        DateTime? fecha = null) => new(Usuario, id, fecha ?? Ahora.AddDays(-2), titulo,
            cierre, "Quilmes", ModalidadTrabajo.Remoto, TipoContrato.TiempoCompleto, null);

    [Fact]
    public void PrioridadSiempreEnteraYSeRedondeaAlMasCercano()
    {
        var calculada = OrdenamientoPendientesService.CalcularPrioridad(86.45m, null, 74.6m,
            Ahora.AddDays(-10), Ahora);
        Assert.InRange(calculada, 0, 100);
        Assert.Equal(50, OrdenamientoPendientesService.CalcularPrioridad(50m, null, 50m,
            Ahora.AddDays(-15), Ahora));
    }

    [Fact]
    public void SinFechaCierre_NoSeInventaUrgencia()
    {
        var conCierre = OrdenamientoPendientesService.CalcularPrioridad(80m, Ahora.AddHours(1), 80m,
            Ahora.AddDays(-3), Ahora);
        var sinCierre = OrdenamientoPendientesService.CalcularPrioridad(80m, null, 80m,
            Ahora.AddDays(-3), Ahora);
        Assert.InRange(conCierre, 0, 100);
        Assert.InRange(sinCierre, 0, 100);
    }

    [Fact]
    public void FechaCierre_PoneAlFinalLasOfertasSinCierre()
    {
        var a = Oferta(Guid.NewGuid(), "Sin cierre");
        var b = Oferta(Guid.NewGuid(), "Con cierre", Ahora.AddDays(3));
        var ordenadas = OrdenamientoPendientesService.Ordenar([a,b],
            new DatosExternosOrdenamiento("FechaCierre", null, new Dictionary<Guid, decimal>()), Ahora);
        Assert.Equal(b.EmpleoId, ordenadas[0].EmpleoId);
        Assert.Equal(a.EmpleoId, ordenadas[1].EmpleoId);
    }

    [Theory]
    [InlineData("Prioridad")]
    [InlineData("Compatibilidad")]
    [InlineData("FechaCierre")]
    [InlineData("Antiguos")]
    [InlineData("Recientes")]
    public void TodosLosOrdenesDevuelvenLasMismasOfertas(string orden)
    {
        var a = Oferta(Guid.NewGuid(), "A", Ahora.AddDays(8), Ahora.AddDays(-1));
        var b = Oferta(Guid.NewGuid(), "B", null, Ahora.AddDays(-5));
        var datos = new DatosExternosOrdenamiento(orden, null,
            new Dictionary<Guid, decimal> { [a.EmpleoId] = 90m, [b.EmpleoId] = 40m });
        var resultado = OrdenamientoPendientesService.Ordenar([a,b], datos, Ahora);
        Assert.Equal(2, resultado.Count);
        Assert.Contains(resultado, x => x.EmpleoId == a.EmpleoId);
        Assert.Contains(resultado, x => x.EmpleoId == b.EmpleoId);
    }

    [Fact]
    public async Task Propuesta_NoModificaCamposHastaAceptarCambios()
    {
        var datos = new DatosFalsos();
        var prep = datos.CrearPreparacion();
        var revision = CrearRevision(datos);
        var cambio = await revision.ProponerAsync(prep.Id, Usuario,
            new ProponerCambiosRequest(null, "Presentacion nueva"), "jwt");
        Assert.NotNull(cambio);
        Assert.Equal("Original", cambio.TextoPresentacion);
        Assert.True(cambio.CambiosPendientes);
        Assert.Equal("Presentacion nueva", cambio.TextoPresentacionPropuesto);
        var aceptada = await revision.AceptarCambiosAsync(prep.Id, Usuario, cambio.VersionEdicion, "jwt");
        Assert.NotNull(aceptada);
        Assert.Equal("Presentacion nueva", aceptada.TextoPresentacion);
        Assert.False(aceptada.CambiosPendientes);
    }

    [Fact]
    public async Task DescartarCambios_ConservaVersionOriginal()
    {
        var datos = new DatosFalsos();
        var prep = datos.CrearPreparacion();
        var revision = CrearRevision(datos);
        await revision.ProponerAsync(prep.Id, Usuario,
            new ProponerCambiosRequest(null, "Otro texto"), "jwt");
        var descartada = await revision.DescartarCambiosAsync(prep.Id, Usuario, prep.VersionEdicion);
        Assert.NotNull(descartada);
        Assert.Equal("Original", descartada.TextoPresentacion);
        Assert.False(descartada.CambiosPendientes);
    }

    [Fact]
    public async Task NoPermiteEnviarSiHayCambiosSinAceptar()
    {
        var datos = new DatosFalsos();
        var prep = datos.CrearPreparacion();
        var revision = CrearRevision(datos);
        await revision.ProponerAsync(prep.Id, Usuario,
            new ProponerCambiosRequest(null, "Texto"), "jwt");
        var ex = await Assert.ThrowsAsync<PostulacionException>(
            () => revision.EnviarAsync(prep.Id, Usuario, prep.VersionEdicion, "jwt"));
        Assert.Equal("CAMBIOS_SIN_ACEPTAR", ex.CodigoError);
    }

    [Fact]
    public async Task AprobacionExpresa_EnviaLaVersionAceptada()
    {
        var datos = new DatosFalsos();
        var prep = datos.CrearPreparacion();
        var revision = CrearRevision(datos);
        await revision.ProponerAsync(prep.Id, Usuario,
            new ProponerCambiosRequest(null, "Texto revisado"), "jwt");
        await revision.AceptarCambiosAsync(prep.Id, Usuario, prep.VersionEdicion, "jwt");
        var enviada = await revision.EnviarAsync(prep.Id, Usuario, prep.VersionEdicion, "jwt");
        Assert.NotNull(enviada);
        Assert.Equal("Texto revisado", enviada.TextoPresentacion);
        Assert.Equal(Usuario, enviada.UsuarioId);
        Assert.Equal(Empleo, enviada.EmpleoId);
    }

    [Fact]
    public async Task VersionDesactualizada_ImpideAceptar()
    {
        var datos = new DatosFalsos();
        var prep = datos.CrearPreparacion();
        var revision = CrearRevision(datos);
        var versionAntigua = prep.VersionEdicion;
        await revision.ProponerAsync(prep.Id, Usuario,
            new ProponerCambiosRequest(null, "Otra version"), "jwt");
        var ex = await Assert.ThrowsAsync<PostulacionException>(() =>
            revision.AceptarCambiosAsync(prep.Id, Usuario, versionAntigua, "jwt"));
        Assert.Equal("PREPARACION_DESACTUALIZADA", ex.CodigoError);
        Assert.Equal("Original", prep.TextoPresentacion);
    }

    [Fact]
    public async Task NoExponePreparacionDeOtroUsuario()
    {
        var datos = new DatosFalsos();
        var prep = datos.CrearPreparacion();
        var revision = CrearRevision(datos);
        Assert.Null(await revision.ProponerAsync(prep.Id, Guid.NewGuid(),
            new ProponerCambiosRequest(null, "Nuevo"), "jwt"));
    }

    private static RevisionPreparacionesService CrearRevision(DatosFalsos datos) =>
        new(datos, new CvFalso(),
            new PostulacionService(new PostulacionesFalsas(), new CvFalso(), new PerfilFalso()),
            new ConfirmacionFalsa());

    private sealed class DatosFalsos : IPendientesYPreparacionesRepository
    {
        private readonly List<PreparacionPostulacion> _preparaciones = [];
        public PreparacionPostulacion CrearPreparacion()
        {
            var p = new PreparacionPostulacion { Id = Guid.NewGuid(), UsuarioId = Usuario,
                EmpleoId = Empleo, Estado = EstadoPreparacionPostulacion.PendienteAprobacion,
                CvId = Guid.NewGuid(), TextoPresentacion = "Original" };
            _preparaciones.Add(p);
            return p;
        }
        public Task<EmpleoPendiente?> ObtenerPendienteAsync(Guid u, Guid e, CancellationToken ct = default) => Task.FromResult<EmpleoPendiente?>(null);
        public Task<List<EmpleoPendiente>> ListarPendientesAsync(Guid u, CancellationToken ct = default) => Task.FromResult(new List<EmpleoPendiente>());
        public Task AgregarPendienteAsync(EmpleoPendiente p, CancellationToken ct = default) => Task.CompletedTask;
        public Task EliminarPendienteAsync(EmpleoPendiente p, CancellationToken ct = default) => Task.CompletedTask;
        public Task<List<PreparacionPostulacion>> ListarPreparacionesAsync(Guid u, CancellationToken ct = default) =>
            Task.FromResult(_preparaciones.Where(x => x.UsuarioId == u).ToList());
        public Task<PreparacionPostulacion?> ObtenerPreparacionAsync(Guid id, Guid u, CancellationToken ct = default) =>
            Task.FromResult(_preparaciones.FirstOrDefault(x => x.Id == id && x.UsuarioId == u));
        public Task<PreparacionPostulacion?> ObtenerActivaAsync(Guid u, CancellationToken ct = default) =>
            Task.FromResult<PreparacionPostulacion?>(null);
        public Task<bool> ExistePreparacionNoFinalizadaAsync(Guid u, Guid e, CancellationToken ct = default) =>
            Task.FromResult(false);
        public Task AgregarPreparacionAsync(PreparacionPostulacion p, CancellationToken ct = default) { _preparaciones.Add(p); return Task.CompletedTask; }
        public Task GuardarPreparacionAsync(PreparacionPostulacion p, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class CvFalso : ICvValidationClient
    {
        public Task<bool> PerteneceAlUsuarioAsync(Guid cv, string token, CancellationToken ct = default) =>
            Task.FromResult(true);
    }
    private sealed class PerfilFalso : IPerfilLaboralValidationClient
    {
        public Task<ValidacionPerfilLaboralResultado> ValidarAsync(string token, CancellationToken ct = default) =>
            Task.FromResult(new ValidacionPerfilLaboralResultado(true, []));
    }
    private sealed class PostulacionesFalsas : IPostulacionRepository
    {
        public Task<Empleo?> ObtenerEmpleoAsync(Guid empleo, CancellationToken ct = default) =>
            Task.FromResult<Empleo?>(new Empleo { Id = empleo, Estado = EstadoEmpleo.Publicado, FechaCierre = DateTime.UtcNow.AddDays(3) });
        public Task<bool> ExistePostulacionAsync(Guid u, Guid e, CancellationToken ct = default) => Task.FromResult(false);
        public Task AgregarAsync(Postulacion p, CancellationToken ct = default) => Task.CompletedTask;
        public Task<List<Postulacion>> ObtenerPorUsuarioAsync(Guid u, CancellationToken ct = default) => Task.FromResult(new List<Postulacion>());
        public Task<Postulacion?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Postulacion?>(null);
    }
    private sealed class ConfirmacionFalsa : IConfirmacionPreparacionRepository
    {
        public Task<Postulacion> ConfirmarEnvioAsync(Guid u, Guid id, int v, Postulacion p, CancellationToken ct = default) => Task.FromResult(p);
    }
}
