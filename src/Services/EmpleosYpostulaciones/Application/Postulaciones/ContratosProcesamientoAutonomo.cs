using Domain.Entities;

namespace Application.Postulaciones;

public sealed record PreferenciasProcesamiento(
    List<string>? PuestosInteres, List<string>? UbicacionesLaborales,
    List<string>? ModalidadesTrabajo, List<string>? TiposContrato,
    decimal? ExpectativaSalarial, List<string>? Exclusiones);

public sealed record ConfiguracionProcesamiento(
    string? Modo, int CompatibilidadMinima, int LimiteDiario,
    PreferenciasProcesamiento? Preferencias, string? OrdenPendientes);

public interface IConsultasProcesamientoInterno
{
    Task<ConfiguracionProcesamiento?> ConfiguracionAsync(Guid usuarioId, CancellationToken ct = default);
    Task<ValidacionPerfilLaboralResultado?> ValidarPerfilAsync(Guid usuarioId, CancellationToken ct = default);
    Task<bool> ValidarCvAsync(Guid usuarioId, Guid cvId, CancellationToken ct = default);
}

public interface IDatosExternosOrdenamientoInterno
{
    Task<DatosExternosOrdenamiento> ObtenerInternoAsync(Guid usuarioId, CancellationToken ct = default);
}

// El Commit 9 conectara Chatbot a este contrato. Una implementacion no configurada
// NO debe iniciar preparaciones ni enviar postulaciones de prueba.
public sealed record ResultadoPreparacionIA(Guid? CvId, string? TextoPresentacion,
    bool RequiereAprobacionCv, string? CvTextoPropuesto = null,
    string? CvTituloPropuesto = null, long? CvVersionOriginal = null, bool CrearCvNuevo = false);
public interface IPreparadorPostulacionesIA
{
    bool Disponible { get; }
    Task<ResultadoPreparacionIA> PrepararAsync(Guid usuarioId, Guid empleoId, CancellationToken ct = default);
    Task<ResultadoPreparacionIA> ModificarAsync(Guid usuarioId, Guid empleoId,
        string instrucciones, Guid? cvAnterior, string? textoAnterior, CancellationToken ct = default) =>
        throw new NotSupportedException("Este preparador no admite cambios solicitados por el usuario.");
}

public sealed record IntentoProcesamientoAutonomo(
    Guid UsuarioId, Guid EmpleoId, Guid? PreparacionId,
    string Resultado, string? Motivo, DateTime FechaUtc);

public interface IRegistroProcesamientoAutonomo
{
    Task<List<Guid>> UsuariosConPendientesAsync(CancellationToken ct = default);
    Task<int> CupoConsumidoAsync(Guid usuarioId, DateTime inicioUtc, DateTime finUtc, CancellationToken ct = default);
    Task<int> IntentosEnPeriodoAsync(Guid usuarioId, Guid empleoId, DateTime desdeUtc, CancellationToken ct = default);
    Task<Empleo?> ObtenerOfertaAsync(Guid empleoId, CancellationToken ct = default);
    Task<PreparacionPostulacion?> EstadoActualAsync(Guid preparacionId, Guid usuarioId, CancellationToken ct = default);
    Task GuardarPreparacionAsync(PreparacionPostulacion preparacion, CancellationToken ct = default);
    Task RegistrarIntentoAsync(IntentoProcesamientoAutonomo intento, CancellationToken ct = default);
    Task<int> RecuperarInterrumpidasAsync(DateTime limiteUtc, CancellationToken ct = default);
}
