using Domain.Entities;
using Domain.Enums;

namespace Application.Postulaciones;

public sealed class ProcesamientoAutonomoService(
    IRegistroProcesamientoAutonomo registro,
    IConsultasProcesamientoInterno consultas,
    OrdenamientoPendientesService ordenamiento,
    PreparacionesService preparaciones,
    IPreparadorPostulacionesIA preparador,
    PostulacionService postulaciones,
    IConfirmacionPreparacionRepository confirmacion)
{
    // UTC para definir la ventana diaria de consumo, sin depender del servidor.
    public static (DateTime Inicio, DateTime Fin) DiaUtc(DateTime ahora)
    {
        var inicio = DateTime.SpecifyKind(ahora.Date, DateTimeKind.Utc);
        return (inicio, inicio.AddDays(1));
    }

    public static bool Permitido(string? modo) => modo is "Automatica" or "Asistida";

    public static bool Excluida(Empleo oferta, IReadOnlyList<string>? exclusiones)
    {
        var texto = string.Join(" ", new string?[] { oferta.Titulo, oferta.Descripcion,
            oferta.Requisitos, oferta.Ubicacion, oferta.Modalidad.ToString(), oferta.TipoContrato.ToString() });
        return exclusiones?.Any(x => !string.IsNullOrWhiteSpace(x) &&
            texto.Contains(x.Trim(), StringComparison.OrdinalIgnoreCase)) == true;
    }

    public async Task<int> EjecutarCicloAsync(CancellationToken ct = default)
    {
        // Nunca reservar trabajo si no existe el adaptador real del agente.
        if (!preparador.Disponible) return 0;
        var ahora = DateTime.UtcNow;
        var (inicio, fin) = DiaUtc(ahora);
        var completadas = 0;
        foreach (var usuarioId in await registro.UsuariosConPendientesAsync(ct))
        {
            ct.ThrowIfCancellationRequested();
            var config = await consultas.ConfiguracionAsync(usuarioId, ct);
            if (config is null || !Permitido(config.Modo)) continue;
            if (config.LimiteDiario is < 1 or > 10 || config.CompatibilidadMinima is < 1 or > 100)
                continue;
            if (await registro.CupoConsumidoAsync(usuarioId, inicio, fin, ct) >= config.LimiteDiario)
                continue;
            var perfil = await consultas.ValidarPerfilAsync(usuarioId, ct);
            if (perfil?.PerfilCompleto != true) continue;
            var pendientes = await ordenamiento.ListarInternoAsync(usuarioId, ct);
            foreach (var pendiente in pendientes)
            {
                ct.ThrowIfCancellationRequested();
                if (pendiente.Compatibilidad is null ||
                    pendiente.Compatibilidad.Value < config.CompatibilidadMinima) continue;
                var oferta = await registro.ObtenerOfertaAsync(pendiente.EmpleoId, ct);
                if (oferta is null || oferta.Estado != EstadoEmpleo.Publicado ||
                    (oferta.FechaCierre.HasValue && oferta.FechaCierre <= DateTime.UtcNow) ||
                    Excluida(oferta, config.Preferencias?.Exclusiones)) continue;
                if (await registro.IntentosEnPeriodoAsync(usuarioId, oferta.Id, ahora.AddHours(-24), ct) >= 3)
                    continue;
                PreparacionPostulacion preparacion;
                try
                {
                    // Chequeo previo y restriccion unica de PostgreSQL ante carreras.
                    preparacion = await preparaciones.RegistrarInicioAsync(
                        usuarioId, oferta.Id, OrigenPreparacionPostulacion.IniciativaIA, ct);
                }
                catch (PostulacionException ex) when (ex.CodigoError is
                    "POSTULACION_DUPLICADA" or "IA_OCUPADA" or "PREPARACION_DUPLICADA" or
                    "OFERTA_NO_DISPONIBLE" or "OFERTA_FINALIZADA" or "OFERTA_NO_ENCONTRADA")
                {
                    continue;
                }

                preparacion.ModoEjecucion = config.Modo;
                try
                {
                    await registro.GuardarPreparacionAsync(preparacion, ct);
                    var resultado = await preparador.PrepararAsync(usuarioId, oferta.Id, ct);
                    var actual = await registro.EstadoActualAsync(preparacion.Id, usuarioId, ct);
                    if (actual is null) throw new InvalidOperationException("Preparacion desaparecida.");
                    if (actual.Estado == EstadoPreparacionPostulacion.CancelacionSolicitada)
                    {
                        actual.Estado = EstadoPreparacionPostulacion.Cancelada;
                        actual.FechaActualizacion = DateTime.UtcNow;
                        await registro.GuardarPreparacionAsync(actual, ct);
                        await RegistrarSinBloquear(usuarioId, oferta.Id, actual.Id, "Cancelada", null, ct);
                        break;
                    }
                    if (actual.Estado != EstadoPreparacionPostulacion.Preparando)
                        throw new InvalidOperationException("La preparacion cambio de estado.");
                    if (resultado.TextoPresentacion is { Length: > 4000 })
                        throw PostulacionException.PresentacionInvalida();
                    if (resultado.CvId == Guid.Empty)
                        throw PostulacionException.CvNoEncontrado();
                    actual.CvId = resultado.CvId;
                    actual.TextoPresentacion = resultado.TextoPresentacion;
                    actual.Estado = resultado.RequiereAprobacionCv || !resultado.CvId.HasValue
                        ? EstadoPreparacionPostulacion.PendienteAprobacionCv
                        : EstadoPreparacionPostulacion.PendienteAprobacion;
                    actual.FechaActualizacion = DateTime.UtcNow;
                    // Sin aprobar cambios sobre el CV, jamas se envia.
                    if (actual.Estado == EstadoPreparacionPostulacion.PendienteAprobacionCv)
                    {
                        await registro.GuardarPreparacionAsync(actual, ct);
                        await RegistrarSinBloquear(usuarioId, oferta.Id, actual.Id, "RequiereCv", null, ct);
                        completadas++;
                        break;
                    }
                    if (config.Modo == "Asistida")
                    {
                        // Propuesta terminada: consume un cupo del dia, IA libre.
                        actual.ContabilizadaEnCupo = true;
                        actual.FechaContabilizacionUtc = DateTime.UtcNow;
                        await registro.GuardarPreparacionAsync(actual, ct);
                        await RegistrarSinBloquear(usuarioId, oferta.Id, actual.Id, "PropuestaLista", null, ct);
                        completadas++;
                        break;
                    }
                    await registro.GuardarPreparacionAsync(actual, ct);
                    // Revalidar configuracion y cupo ANTES del envio automatico.
                    var vigente = await consultas.ConfiguracionAsync(usuarioId, ct);
                    if (vigente is null || vigente.Modo != "Automatica" ||
                        await registro.CupoConsumidoAsync(usuarioId, inicio, fin, ct) >= vigente.LimiteDiario)
                    {
                        await RegistrarSinBloquear(usuarioId, oferta.Id, actual.Id, "PendienteRevision",
                            "El modo o cupo cambio antes del envio.", ct);
                        break;
                    }
                    if (actual.CvId is not { } cvId) break;
                    var postulacion = await postulaciones.ValidarYConstruirInternoAsync(
                        new CrearPostulacionRequest(usuarioId, oferta.Id, cvId, actual.TextoPresentacion), ct);
                    postulacion.Origen = "Automatica";
                    // La escritura, el descuento del cupo y el cierre de preparacion son atomicos.
                    await confirmacion.ConfirmarEnvioAsync(usuarioId, actual.Id,
                        actual.VersionEdicion, postulacion, ct);
                    await RegistrarSinBloquear(usuarioId, oferta.Id, actual.Id, "Completada", null, ct);
                    completadas++;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // No anular ni reetiquetar un envio que se haya confirmado.
                    var vigente = await registro.EstadoActualAsync(preparacion.Id, usuarioId,
                        CancellationToken.None);
                    if (vigente is not null && vigente.Estado != EstadoPreparacionPostulacion.Enviada)
                    {
                        vigente.Estado = vigente.Estado == EstadoPreparacionPostulacion.CancelacionSolicitada
                            ? EstadoPreparacionPostulacion.Cancelada : EstadoPreparacionPostulacion.Fallida;
                        vigente.UltimoError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                        vigente.FechaActualizacion = DateTime.UtcNow;
                        await registro.GuardarPreparacionAsync(vigente, CancellationToken.None);
                    }
                    await RegistrarSinBloquear(usuarioId, oferta.Id, preparacion.Id, "Fallida",
                        ex.Message, CancellationToken.None);
                }
                // Como maximo un trabajo por usuario y ciclo; no colas paralelas.
                break;
            }
        }
        return completadas;
    }

    private async Task RegistrarSinBloquear(Guid usuarioId, Guid empleoId, Guid preparacionId,
        string estado, string? mensaje, CancellationToken ct)
    {
        try
        {
            await registro.RegistrarIntentoAsync(new IntentoProcesamientoAutonomo(
                usuarioId, empleoId, preparacionId, estado, mensaje, DateTime.UtcNow), ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // El registro no puede deshacer una postulacion confirmada.
        }
    }
}
