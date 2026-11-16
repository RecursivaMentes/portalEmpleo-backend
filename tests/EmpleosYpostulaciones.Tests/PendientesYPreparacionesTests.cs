using Application.Postulaciones;
using Domain.Entities;
using Domain.Enums;
using Xunit;

namespace EmpleosYpostulaciones.Tests;

public sealed class PendientesYPreparacionesTests
{
    private readonly Guid _usuario = Guid.NewGuid();
    private readonly Guid _empleo = Guid.NewGuid();

    [Fact]
    public async Task GuardarOferta_NoCreaPostulacionNiExigeCv()
    {
        var datos = new DatosFalsos();
        var envio = new PostulacionesFalsas { Oferta = OfertaValida() };
        var resultado = await new PendientesService(datos, envio).AgregarAsync(_usuario, _empleo);
        Assert.Equal(_usuario, resultado.UsuarioId);
        Assert.Equal(_empleo, resultado.EmpleoId);
        Assert.Single(datos.Pendientes);
        Assert.Empty(envio.Registradas);
    }

    [Fact]
    public async Task GuardarOfertaDuplicada_Rechaza()
    {
        var datos = new DatosFalsos();
        var servicio = new PendientesService(datos, new PostulacionesFalsas { Oferta = OfertaValida() });
        await servicio.AgregarAsync(_usuario, _empleo);
        var ex = await Assert.ThrowsAsync<PostulacionException>(() => servicio.AgregarAsync(_usuario, _empleo));
        Assert.Equal("PENDIENTE_DUPLICADO", ex.CodigoError);
    }

    [Fact]
    public async Task OfertaVencida_NoSeGuarda()
    {
        var datos = new DatosFalsos();
        var oferta = OfertaValida();
        oferta.FechaCierre = DateTime.UtcNow.AddMinutes(-2);
        var servicio = new PendientesService(datos, new PostulacionesFalsas { Oferta = oferta });
        await Assert.ThrowsAsync<PostulacionException>(() => servicio.AgregarAsync(_usuario, _empleo));
        Assert.Empty(datos.Pendientes);
    }

    [Fact]
    public async Task PostulacionEnviada_NoSeGuardaNiPrepara()
    {
        var datos = new DatosFalsos();
        var post = new PostulacionesFalsas { Oferta = OfertaValida(), YaPostulado = true };
        await Assert.ThrowsAsync<PostulacionException>(() => new PendientesService(datos, post).AgregarAsync(_usuario, _empleo));
        await Assert.ThrowsAsync<PostulacionException>(() => new PreparacionesService(datos, post)
            .RegistrarInicioAsync(_usuario, _empleo, OrigenPreparacionPostulacion.SolicitudUsuario));
        Assert.Empty(datos.Preparaciones);
    }

    [Fact]
    public async Task SoloDevuelveReferenciasDelUsuario()
    {
        var datos = new DatosFalsos();
        datos.Pendientes.Add(new EmpleoPendiente { UsuarioId = _usuario, EmpleoId = _empleo });
        datos.Pendientes.Add(new EmpleoPendiente { UsuarioId = Guid.NewGuid(), EmpleoId = Guid.NewGuid() });
        var servicio = new PendientesService(datos, new PostulacionesFalsas());
        Assert.Single(await servicio.ListarAsync(_usuario));
        Assert.Null(await servicio.ObtenerAsync(Guid.NewGuid(), _empleo));
        Assert.False(await servicio.EliminarAsync(Guid.NewGuid(), _empleo));
        Assert.Single(await servicio.ListarAsync(_usuario));
        Assert.True(await servicio.EliminarAsync(_usuario, _empleo));
        Assert.Empty(await servicio.ListarAsync(_usuario));
    }

    [Fact]
    public async Task IaOcupada_RechazaSegundaPreparacion()
    {
        var datos = new DatosFalsos();
        var servicio = new PreparacionesService(datos, new PostulacionesFalsas { Oferta = OfertaValida() });
        await servicio.RegistrarInicioAsync(_usuario, _empleo, OrigenPreparacionPostulacion.SolicitudUsuario);
        var ex = await Assert.ThrowsAsync<PostulacionException>(() => servicio.RegistrarInicioAsync(
            _usuario, Guid.NewGuid(), OrigenPreparacionPostulacion.IniciativaIA));
        Assert.Equal("IA_OCUPADA", ex.CodigoError);
        Assert.Single(datos.Preparaciones);
    }

    [Fact]
    public async Task EsperandoAprobacion_IaDisponiblePeroMismaOfertaNoSeDuplica()
    {
        var datos = new DatosFalsos();
        var servicio = new PreparacionesService(datos, new PostulacionesFalsas { Oferta = OfertaValida() });
        var primera = await servicio.RegistrarInicioAsync(_usuario, _empleo, OrigenPreparacionPostulacion.IniciativaIA);
        primera.Estado = EstadoPreparacionPostulacion.PendienteAprobacion;
        var disponibilidad = await servicio.DisponibilidadAsync(_usuario);
        Assert.True(disponibilidad.Disponible);
        var ex = await Assert.ThrowsAsync<PostulacionException>(() => servicio.RegistrarInicioAsync(
            _usuario, _empleo, OrigenPreparacionPostulacion.SolicitudUsuario));
        Assert.Equal("PREPARACION_DUPLICADA", ex.CodigoError);
    }

    [Fact]
    public async Task AplicandoCv_MantieneOcupadaLaEjecucion()
    {
        var datos = new DatosFalsos();
        var servicio = new PreparacionesService(datos,
            new PostulacionesFalsas { Oferta = OfertaValida() });
        var prep = await servicio.RegistrarInicioAsync(_usuario, _empleo,
            OrigenPreparacionPostulacion.SolicitudUsuario);
        prep.Estado = EstadoPreparacionPostulacion.AplicandoCv;
        Assert.False((await servicio.DisponibilidadAsync(_usuario)).Disponible);
    }

    [Fact]
    public async Task CancelarPreparacionActiva_NoLaLiberaAntesDeDetenerTrabajador()
    {
        var datos = new DatosFalsos();
        var servicio = new PreparacionesService(datos, new PostulacionesFalsas { Oferta = OfertaValida() });
        var prep = await servicio.RegistrarInicioAsync(_usuario, _empleo, OrigenPreparacionPostulacion.SolicitudUsuario);
        var cancelada = await servicio.CancelarAsync(prep.Id, _usuario);
        Assert.Equal(EstadoPreparacionPostulacion.CancelacionSolicitada, cancelada!.Estado);
        Assert.False((await servicio.DisponibilidadAsync(_usuario)).Disponible);
    }

    [Fact]
    public async Task CancelarPendienteAprobacion_LiberaOferta()
    {
        var datos = new DatosFalsos();
        var servicio = new PreparacionesService(datos, new PostulacionesFalsas { Oferta = OfertaValida() });
        var prep = await servicio.RegistrarInicioAsync(_usuario, _empleo, OrigenPreparacionPostulacion.SolicitudUsuario);
        prep.Estado = EstadoPreparacionPostulacion.PendienteAprobacion;
        var cancelada = await servicio.CancelarAsync(prep.Id, _usuario);
        Assert.Equal(EstadoPreparacionPostulacion.Cancelada, cancelada!.Estado);
        Assert.True((await servicio.DisponibilidadAsync(_usuario)).Disponible);
    }

    [Fact]
    public async Task PreparacionAjena_NoSePuedeConsultarNiCancelar()
    {
        var datos = new DatosFalsos();
        var servicio = new PreparacionesService(datos, new PostulacionesFalsas { Oferta = OfertaValida() });
        var prep = await servicio.RegistrarInicioAsync(_usuario, _empleo, OrigenPreparacionPostulacion.SolicitudUsuario);
        Assert.Null(await servicio.ObtenerAsync(prep.Id, Guid.NewGuid()));
        Assert.Null(await servicio.CancelarAsync(prep.Id, Guid.NewGuid()));
        Assert.Equal(EstadoPreparacionPostulacion.Preparando, prep.Estado);
    }

    [Fact]
    public async Task Enviando_NoSeCancelaNiSeDeclaraCancelada()
    {
        var datos = new DatosFalsos();
        var servicio = new PreparacionesService(datos, new PostulacionesFalsas { Oferta = OfertaValida() });
        var prep = await servicio.RegistrarInicioAsync(_usuario, _empleo, OrigenPreparacionPostulacion.IniciativaIA);
        prep.Estado = EstadoPreparacionPostulacion.Enviando;
        var ex = await Assert.ThrowsAsync<PostulacionException>(() => servicio.CancelarAsync(prep.Id, _usuario));
        Assert.Equal("PREPARACION_NO_CANCELABLE", ex.CodigoError);
    }

    private static Empleo OfertaValida() => new()
    {
        Id = Guid.NewGuid(), Estado = EstadoEmpleo.Publicado,
        FechaCierre = DateTime.UtcNow.AddDays(7)
    };

    private sealed class DatosFalsos : IPendientesYPreparacionesRepository
    {
        public List<EmpleoPendiente> Pendientes { get; } = [];
        public List<PreparacionPostulacion> Preparaciones { get; } = [];

        public Task<EmpleoPendiente?> ObtenerPendienteAsync(Guid usuarioId, Guid empleoId, CancellationToken ct = default) =>
            Task.FromResult(Pendientes.FirstOrDefault(x => x.UsuarioId == usuarioId && x.EmpleoId == empleoId));
        public Task<List<EmpleoPendiente>> ListarPendientesAsync(Guid usuarioId, CancellationToken ct = default) =>
            Task.FromResult(Pendientes.Where(x => x.UsuarioId == usuarioId).ToList());
        public Task AgregarPendienteAsync(EmpleoPendiente pendiente, CancellationToken ct = default)
        { Pendientes.Add(pendiente); return Task.CompletedTask; }
        public Task EliminarPendienteAsync(EmpleoPendiente pendiente, CancellationToken ct = default)
        { Pendientes.Remove(pendiente); return Task.CompletedTask; }
        public Task<List<PreparacionPostulacion>> ListarPreparacionesAsync(Guid usuarioId, CancellationToken ct = default) =>
            Task.FromResult(Preparaciones.Where(x => x.UsuarioId == usuarioId).ToList());
        public Task<PreparacionPostulacion?> ObtenerPreparacionAsync(Guid id, Guid usuarioId, CancellationToken ct = default) =>
            Task.FromResult(Preparaciones.FirstOrDefault(x => x.Id == id && x.UsuarioId == usuarioId));
        public Task<PreparacionPostulacion?> ObtenerActivaAsync(Guid usuarioId, CancellationToken ct = default) =>
            Task.FromResult(Preparaciones.FirstOrDefault(x => x.UsuarioId == usuarioId &&
                x.Estado is EstadoPreparacionPostulacion.Preparando or EstadoPreparacionPostulacion.EnModificacion
                    or EstadoPreparacionPostulacion.AplicandoCv or EstadoPreparacionPostulacion.Enviando or EstadoPreparacionPostulacion.CancelacionSolicitada));
        public Task<bool> ExistePreparacionNoFinalizadaAsync(Guid usuarioId, Guid empleoId, CancellationToken ct = default) =>
            Task.FromResult(Preparaciones.Any(x => x.UsuarioId == usuarioId && x.EmpleoId == empleoId &&
                x.Estado is not (EstadoPreparacionPostulacion.Cancelada or EstadoPreparacionPostulacion.Enviada or EstadoPreparacionPostulacion.Fallida)));
        public Task AgregarPreparacionAsync(PreparacionPostulacion preparacion, CancellationToken ct = default)
        { Preparaciones.Add(preparacion); return Task.CompletedTask; }
        public Task GuardarPreparacionAsync(PreparacionPostulacion preparacion, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class PostulacionesFalsas : IPostulacionRepository
    {
        public Empleo? Oferta { get; set; }
        public bool YaPostulado { get; set; }
        public List<Postulacion> Registradas { get; } = [];
        public Task<Empleo?> ObtenerEmpleoAsync(Guid empleoId, CancellationToken ct = default) => Task.FromResult(Oferta);
        public Task<bool> ExistePostulacionAsync(Guid usuarioId, Guid empleoId, CancellationToken ct = default) => Task.FromResult(YaPostulado);
        public Task AgregarAsync(Postulacion postulacion, CancellationToken ct = default)
        { Registradas.Add(postulacion); return Task.CompletedTask; }
        public Task<List<Postulacion>> ObtenerPorUsuarioAsync(Guid usuarioId, CancellationToken ct = default) =>
            Task.FromResult(Registradas.Where(x => x.UsuarioId == usuarioId).ToList());
        public Task<Postulacion?> ObtenerPorIdAsync(Guid postulacionId, CancellationToken ct = default) =>
            Task.FromResult(Registradas.FirstOrDefault(x => x.Id == postulacionId));
    }
}
