using Application.Postulaciones;
using Domain.Entities;
using Domain.Enums;
using Xunit;

namespace EmpleosYpostulaciones.Tests;

public sealed class ReutilizacionSolicitudIA_Tests
{
    [Fact]
    public async Task MismaOfertaConPreparacionExistente_NoVuelveALlamarIA()
    {
        var usuario = Guid.NewGuid();
        var empleo = Guid.NewGuid();
        var repo = new RepoFalso();
        var preparacion = new PreparacionPostulacion
        {
            Id = Guid.NewGuid(), UsuarioId = usuario, EmpleoId = empleo,
            Estado = EstadoPreparacionPostulacion.PendienteAprobacion
        };
        repo.Preparaciones.Add(preparacion);
        var postulaciones = new PostulacionesFalsas();
        var prepService = new PreparacionesService(repo, postulaciones);
        var agente = new PreparadorFalso();
        var servicio = new SolicitudUsuarioIAService(prepService, repo,
            new ConsultaFalsa(), agente, null!, null!);
        var primera = await servicio.SolicitarAsync(usuario, empleo);
        var segunda = await servicio.SolicitarAsync(usuario, empleo);
        Assert.Same(preparacion, primera);
        Assert.Same(preparacion, segunda);
        Assert.Equal(0, agente.Veces);
    }
    private sealed class PreparadorFalso : IPreparadorPostulacionesIA
    {
        public bool Disponible => true;
        public int Veces { get; private set; }
        public Task<ResultadoPreparacionIA> PrepararAsync(Guid u, Guid e, CancellationToken ct = default)
        {
            Veces++;
            throw new InvalidOperationException("No debe entrar al preparador con una solicitud existente.");
        }
    }
    private sealed class ConsultaFalsa : IConsultasProcesamientoInterno
    {
        public Task<ConfiguracionProcesamiento?> ConfiguracionAsync(Guid u, CancellationToken ct = default) =>
            Task.FromResult<ConfiguracionProcesamiento?>(new("Asistida", 60, 5, null, "Prioridad"));
        public Task<ValidacionPerfilLaboralResultado?> ValidarPerfilAsync(Guid u, CancellationToken ct = default) =>
            Task.FromResult<ValidacionPerfilLaboralResultado?>(new(true, []));
        public Task<bool> ValidarCvAsync(Guid u, Guid c, CancellationToken ct = default) => Task.FromResult(true);
    }
    private sealed class RepoFalso : IPendientesYPreparacionesRepository
    {
        public List<PreparacionPostulacion> Preparaciones { get; } = [];
        public Task<List<PreparacionPostulacion>> ListarPreparacionesAsync(Guid u, CancellationToken ct = default) =>
            Task.FromResult(Preparaciones.Where(x => x.UsuarioId == u).ToList());
        public Task<PreparacionPostulacion?> ObtenerPreparacionAsync(Guid id, Guid u, CancellationToken ct = default) =>
            Task.FromResult(Preparaciones.FirstOrDefault(x => x.Id == id && x.UsuarioId == u));
        public Task<PreparacionPostulacion?> ObtenerActivaAsync(Guid u, CancellationToken ct = default) => Task.FromResult<PreparacionPostulacion?>(null);
        public Task<bool> ExistePreparacionNoFinalizadaAsync(Guid u, Guid e, CancellationToken ct = default) => Task.FromResult(false);
        public Task AgregarPreparacionAsync(PreparacionPostulacion item, CancellationToken ct = default) { Preparaciones.Add(item); return Task.CompletedTask; }
        public Task GuardarPreparacionAsync(PreparacionPostulacion item, CancellationToken ct = default) => Task.CompletedTask;
        public Task<EmpleoPendiente?> ObtenerPendienteAsync(Guid u, Guid e, CancellationToken ct = default) => Task.FromResult<EmpleoPendiente?>(null);
        public Task<List<EmpleoPendiente>> ListarPendientesAsync(Guid u, CancellationToken ct = default) => Task.FromResult(new List<EmpleoPendiente>());
        public Task AgregarPendienteAsync(EmpleoPendiente p, CancellationToken ct = default) => Task.CompletedTask;
        public Task EliminarPendienteAsync(EmpleoPendiente p, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class PostulacionesFalsas : IPostulacionRepository
    {
        public Task<Empleo?> ObtenerEmpleoAsync(Guid e, CancellationToken ct = default) => Task.FromResult<Empleo?>(new Empleo { Id = e, Estado = EstadoEmpleo.Publicado });
        public Task<bool> ExistePostulacionAsync(Guid u, Guid e, CancellationToken ct = default) => Task.FromResult(false);
        public Task AgregarAsync(Postulacion p, CancellationToken ct = default) => Task.CompletedTask;
        public Task<List<Postulacion>> ObtenerPorUsuarioAsync(Guid u, CancellationToken ct = default) => Task.FromResult(new List<Postulacion>());
        public Task<Postulacion?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Postulacion?>(null);
    }
}