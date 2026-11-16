using Application.Postulaciones;
using Domain.Entities;
using Domain.Enums;
using Xunit;

namespace EmpleosYpostulaciones.Tests;

public sealed class RecuperacionAprobacionCvIA_Tests
{
    [Fact]
    public async Task RespuestaPerdidaDeUsuarios_RetieneOperacionYRecuperaSinDuplicar()
    {
        var prep = new PreparacionPostulacion
        {
            Id = Guid.NewGuid(), UsuarioId = Guid.NewGuid(), EmpleoId = Guid.NewGuid(),
            Estado = EstadoPreparacionPostulacion.PendienteAprobacionCv,
            CvNuevoIAPropuesto = true, CvTituloIAPropuesto = "CV propuesto",
            CvTextoIAPropuesto = "Experiencia laboral real confirmada en el perfil.",
            ModoEjecucion = "Desactivada"
        };
        var repo = new RepositorioFalso(prep);
        var app = new AplicacionFalsa();
        var servicio = new AprobacionCvIAService(repo, app, new ConsultasFalsas(),
            null!, null!, null!, null!);
        await Assert.ThrowsAsync<HttpRequestException>(() => servicio.AprobarAsync(
            prep.Id, prep.UsuarioId, prep.VersionEdicion));
        Assert.Equal(EstadoPreparacionPostulacion.AplicandoCv, prep.Estado);
        Assert.Single(app.Operaciones);
        var ok = await servicio.AprobarAsync(prep.Id, prep.UsuarioId, prep.VersionEdicion);
        Assert.NotNull(ok);
        Assert.Equal(EstadoPreparacionPostulacion.PendienteAprobacion, ok.Estado);
        Assert.Single(app.Operaciones);
        Assert.Equal(ok.CvId, app.Operaciones[prep.Id].Id);
    }
    private sealed class AplicacionFalsa : IAplicacionCvAprobado
    {
        public Dictionary<Guid, CvAplicado> Operaciones { get; } = new();
        private bool _perderRespuesta = true;
        public Task<CvAplicado> AplicarAsync(Guid operacionId, Guid usuarioId, Guid? cvId,
            bool crearNuevo, string? titulo, string texto, long? versionOriginal, CancellationToken ct = default)
        {
            if (!Operaciones.TryGetValue(operacionId, out var resultado))
            {
                resultado = new CvAplicado(Guid.NewGuid(), 1);
                Operaciones[operacionId] = resultado;
            }
            if (_perderRespuesta)
            {
                _perderRespuesta = false;
                throw new HttpRequestException("Respuesta perdida despues del commit de Usuarios.");
            }
            return Task.FromResult(resultado);
        }
    }
    private sealed class ConsultasFalsas : IConsultasProcesamientoInterno
    {
        public Task<ConfiguracionProcesamiento?> ConfiguracionAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<ConfiguracionProcesamiento?>(new("Desactivada", 60, 5, null, "Prioridad"));
        public Task<ValidacionPerfilLaboralResultado?> ValidarPerfilAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<ValidacionPerfilLaboralResultado?>(new(true, []));
        public Task<bool> ValidarCvAsync(Guid id, Guid cv, CancellationToken ct = default) => Task.FromResult(true);
    }
    private sealed class RepositorioFalso(PreparacionPostulacion p) : IPendientesYPreparacionesRepository
    {
        public Task<PreparacionPostulacion?> ObtenerPreparacionAsync(Guid id, Guid u, CancellationToken ct = default) =>
            Task.FromResult(id == p.Id && u == p.UsuarioId ? p : null);
        public Task GuardarPreparacionAsync(PreparacionPostulacion preparacion, CancellationToken ct = default) => Task.CompletedTask;
        public Task<PreparacionPostulacion?> ObtenerActivaAsync(Guid u, CancellationToken ct = default) => Task.FromResult<PreparacionPostulacion?>(null);
        public Task<bool> ExistePreparacionNoFinalizadaAsync(Guid u, Guid e, CancellationToken ct = default) => Task.FromResult(false);
        public Task<List<PreparacionPostulacion>> ListarPreparacionesAsync(Guid u, CancellationToken ct = default) => Task.FromResult(new List<PreparacionPostulacion> { p });
        public Task AgregarPreparacionAsync(PreparacionPostulacion item, CancellationToken ct = default) => Task.CompletedTask;
        public Task<EmpleoPendiente?> ObtenerPendienteAsync(Guid u, Guid e, CancellationToken ct = default) => Task.FromResult<EmpleoPendiente?>(null);
        public Task<List<EmpleoPendiente>> ListarPendientesAsync(Guid u, CancellationToken ct = default) => Task.FromResult(new List<EmpleoPendiente>());
        public Task AgregarPendienteAsync(EmpleoPendiente item, CancellationToken ct = default) => Task.CompletedTask;
        public Task EliminarPendienteAsync(EmpleoPendiente item, CancellationToken ct = default) => Task.CompletedTask;
    }
}