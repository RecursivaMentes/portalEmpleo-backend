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
    IConfirmacionPreparacionRepository confirmacion, DiagnosticoAgentePostulaciones? diagnostico = null)
{
    // UTC para definir la ventana diaria de consumo, sin depender del servidor.
    public static (DateTime Inicio, DateTime Fin) DiaUtc(DateTime ahora)
    {
        var inicio = DateTime.SpecifyKind(ahora.Date, DateTimeKind.Utc);
        return (inicio, inicio.AddDays(1));
    }

    public static (DateTime Inicio, DateTime Fin) DiaLocalUtc(DateTime ahoraUtc, string? zonaHoraria)
    {
        var zona = TimeZoneInfo.FindSystemTimeZoneById(
            string.IsNullOrWhiteSpace(zonaHoraria) ? "America/Argentina/Buenos_Aires" : zonaHoraria);
        var local = TimeZoneInfo.ConvertTimeFromUtc(ahoraUtc, zona);
        var inicioLocal = DateTime.SpecifyKind(local.Date, DateTimeKind.Unspecified);
        return (TimeZoneInfo.ConvertTimeToUtc(inicioLocal, zona),
            TimeZoneInfo.ConvertTimeToUtc(inicioLocal.AddDays(1), zona));
    }

    public static bool Permitido(string? modo) => modo is "Automatica" or "Asistida";
    public static bool EnvioSinAprobacionPermitido(string? modo) => modo == "Automatica";

    public static bool Excluida(Empleo oferta, IReadOnlyList<string>? exclusiones)
    {
        var texto = string.Join(" ", new string?[] { oferta.Titulo, oferta.Descripcion,
            oferta.Requisitos, oferta.Ubicacion, oferta.Modalidad.ToString(), oferta.TipoContrato.ToString() });
        return exclusiones?.Any(x => !string.IsNullOrWhiteSpace(x) &&
            texto.Contains(x.Trim(), StringComparison.OrdinalIgnoreCase)) == true;
    }

    // Recomendaciones conserva su orden por compatibilidad. Las preferencias
    // no son filtros adicionales; las exclusiones expresas sí se respetan.
    public static bool Elegible(Empleo oferta, PreferenciasProcesamiento? preferencias, string? fuente,
        DateTime ahoraUtc) => oferta.Estado == EstadoEmpleo.Publicado &&
        (!oferta.FechaCierre.HasValue || oferta.FechaCierre.Value > ahoraUtc) &&
        !Excluida(oferta, preferencias?.Exclusiones);

    public async Task<int> EjecutarCicloAsync(CancellationToken ct = default)
    {
        // Nunca reservar trabajo si no existe el adaptador real del agente.
        if (!preparador.Disponible) return 0;
        var ahora = DateTime.UtcNow;
        var completadas = 0;
        var usuarios = await registro.UsuariosHabilitadosAsync(ct);
        await registro.RegistrarCantidadUsuariosAsync(usuarios.Count, ct);
        foreach (var usuarioId in usuarios)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
            await Informar(usuarioId, "Evaluando", "Buscando ofertas elegibles.", null, ct);
            var config = await consultas.ConfiguracionAsync(usuarioId, ct);
            if (config is null || !Permitido(config.Modo))
            {
                await Informar(usuarioId, "Desactivado", "El agente no está habilitado para esta cuenta.", null, ct);
                continue;
            }
            var (inicio, fin) = DiaLocalUtc(DateTime.UtcNow, config.ZonaHoraria);
            if (config.LimiteDiario is < 1 or > 10 || config.CompatibilidadMinima is < 0 or > 100)
            {
                await Informar(usuarioId, "ConfiguracionInvalida", "Revisá los límites de configuración.", null, ct);
                continue;
            }
            if (await registro.CupoConsumidoAsync(usuarioId, inicio, fin, ct) >= config.LimiteDiario)
            {
                await Informar(usuarioId, "CupoAgotado", "Se alcanzó el límite diario de postulaciones autónomas.", null, ct);
                continue;
            }
            var perfil = await consultas.ValidarPerfilAsync(usuarioId, ct);
            if (perfil?.PerfilCompleto != true)
            {
                await Informar(usuarioId, "PerfilIncompleto", "Completá el perfil laboral para habilitar el agente.", null, ct);
                continue;
            }
            var disponibilidad = await preparaciones.DisponibilidadAsync(usuarioId, ct);
            if (!disponibilidad.Disponible)
            {
                await Informar(usuarioId, "Ocupado", "Ya existe una preparación en curso.", null, ct);
                continue;
            }
            var candidatos = await ordenamiento.ListarCandidatosInternosAsync(usuarioId, config.CompatibilidadMinima, ct);
            var cantidadPendientes = candidatos.Count(x => x.FuenteOferta == PreparacionPostulacion.FuentePendientes);
            var cantidadRecomendaciones = candidatos.Count(x => x.FuenteOferta == PreparacionPostulacion.FuenteRecomendaciones);
            var minimoRecomendaciones = Math.Max(50, config.CompatibilidadMinima);
            await Informar(usuarioId, "Candidatos",
                $"Evaluando {cantidadPendientes} pendientes y {cantidadRecomendaciones} recomendaciones.", candidatos.Count, ct);
            await registro.RegistrarEvaluacionAsync(usuarioId,
                $"Pendientes={cantidadPendientes}; Recomendaciones={cantidadRecomendaciones}; MinimoRecomendaciones={minimoRecomendaciones}%", candidatos.Count, ct);
            var iniciado = false;
            var falloPreparacion = false;
            var descartes = new Dictionary<string, int>();
            void Descartar(string motivo) => descartes[motivo] = descartes.GetValueOrDefault(motivo) + 1;
            foreach (var candidato in candidatos)
            {
                ct.ThrowIfCancellationRequested();
                var oferta = await registro.ObtenerOfertaAsync(candidato.EmpleoId, ct);
                if (oferta is null || !Elegible(oferta, config.Preferencias, candidato.FuenteOferta, DateTime.UtcNow))
                { Descartar("OfertaNoDisponibleOExcluida"); continue; }
                if (await registro.IntentosEnPeriodoAsync(usuarioId, oferta.Id, ahora.AddHours(-24), ct) >= 3)
                { Descartar("MaximoIntentos"); continue; }
                PreparacionPostulacion preparacion;
                try
                {
                    // Chequeo previo y restriccion unica de PostgreSQL ante carreras.
                    preparacion = await preparaciones.RegistrarInicioAsync(
                        usuarioId, oferta.Id, OrigenPreparacionPostulacion.IniciativaIA, ct,
                        candidato.FuenteOferta, config.Modo);
                }
                catch (PostulacionException ex) when (ex.CodigoError is
                    "POSTULACION_DUPLICADA" or "IA_OCUPADA" or "PREPARACION_DUPLICADA" or
                    "OFERTA_NO_DISPONIBLE" or "OFERTA_FINALIZADA" or "OFERTA_NO_ENCONTRADA" or
                    "PENDIENTE_RETIRADO" or "OFERTA_DESCARTADA")
                {
                    Descartar(ex.CodigoError);
                    continue;
                }

                iniciado = true;
                await Informar(usuarioId, "Preparando",
                    candidato.FuenteOferta == PreparacionPostulacion.FuenteRecomendaciones
                        ? "Se inició una preparación desde recomendaciones."
                        : "Se inició una preparación desde pendientes.", candidatos.Count, ct);
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
                    actual.CvTextoIAPropuesto = resultado.CvTextoPropuesto;
                    actual.CvTituloIAPropuesto = resultado.CvTituloPropuesto;
                    actual.CvVersionIAOriginal = resultado.CvVersionOriginal;
                    actual.CvNuevoIAPropuesto = resultado.CrearCvNuevo;
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
                        await registro.GuardarPreparacionAsync(actual, ct);
                        var registrado = await registro.ReservarCupoPropuestaAsync(
                            actual.Id, usuarioId, config.LimiteDiario, inicio, fin, ct);
                        if (!registrado)
                        {
                            actual.Estado = EstadoPreparacionPostulacion.Fallida;
                            actual.UltimoError = "Límite diario alcanzado durante la preparación asistida.";
                            await registro.GuardarPreparacionAsync(actual, ct);
                            break;
                        }
                        await RegistrarSinBloquear(usuarioId, oferta.Id, actual.Id, "PropuestaLista", null, ct);
                        completadas++;
                        break;
                    }
                    await registro.GuardarPreparacionAsync(actual, ct);
                    // Revalidar configuracion y cupo ANTES del envio automatico.
                    var vigente = await consultas.ConfiguracionAsync(usuarioId, ct);
                    if (vigente is not null)
                        (inicio, fin) = DiaLocalUtc(DateTime.UtcNow, vigente.ZonaHoraria);
                    if (vigente is null || vigente.Modo != "Automatica" ||
                        vigente.LimiteDiario is < 1 or > 10 ||
                        vigente.CompatibilidadMinima is < 0 or > 100 ||
                        await registro.CupoConsumidoAsync(usuarioId, inicio, fin, ct) >= vigente.LimiteDiario)
                    {
                        actual.UltimoError = "El modo, la configuracion o el cupo cambio antes del envio.";
                        await registro.GuardarPreparacionAsync(actual, ct);
                        await RegistrarSinBloquear(usuarioId, oferta.Id, actual.Id, "PendienteRevision",
                            actual.UltimoError, ct);
                        break;
                    }
                    // Una preferencia, exclusion o puntuacion puede cambiar mientras la IA prepara.
                    // En tal caso, se conserva el borrador y NO se envia automaticamente.
                    var ofertaVigente = await registro.ObtenerOfertaAsync(oferta.Id, ct);
                    var rankingVigente = await ordenamiento.ListarCandidatosInternosAsync(usuarioId,
                        vigente.CompatibilidadMinima, ct);
                    var candidatoVigente = rankingVigente.FirstOrDefault(x => x.EmpleoId == oferta.Id);
                    if (ofertaVigente is null ||
                        !Elegible(ofertaVigente, vigente.Preferencias, actual.FuenteOferta, DateTime.UtcNow) ||
                        candidatoVigente is null ||
                        (actual.FuenteOferta != PreparacionPostulacion.FuenteRecomendaciones &&
                         candidatoVigente.FuenteOferta != PreparacionPostulacion.FuentePendientes))
                    {
                        actual.UltimoError = "La oferta, compatibilidad o preferencias ya no cumplen los criterios del usuario.";
                        await registro.GuardarPreparacionAsync(actual, ct);
                        await RegistrarSinBloquear(usuarioId, oferta.Id, actual.Id, "PendienteRevision",
                            actual.UltimoError, ct);
                        break;
                    }
                    if (actual.CvId is not { } cvId) break;
                    var postulacion = await postulaciones.ValidarYConstruirInternoAsync(
                        new CrearPostulacionRequest(usuarioId, oferta.Id, cvId, actual.TextoPresentacion),
                        ct, actual.CvVersionIAOriginal);
                    postulacion.Origen = "Automatica";
                    // La escritura, el descuento del cupo y el cierre de preparacion son atomicos.
                    await confirmacion.ConfirmarEnvioConCupoAsync(usuarioId, actual.Id,
                        actual.VersionEdicion, postulacion, vigente.LimiteDiario,
                        inicio, fin, ct);
                    await RegistrarSinBloquear(usuarioId, oferta.Id, actual.Id, "Completada", null, ct);
                    completadas++;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    falloPreparacion = true;
                    diagnostico?.Registrar(usuarioId, "ErrorPreparacion",
                        "No se pudo completar la preparación. Se revisarán las ofertas en el próximo ciclo.");
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
            if (!iniciado)
            {
                var motivos = string.Join(", ", descartes.Select(x => $"{x.Key}={x.Value}"));
                var razones = new List<string>();
                var duplicadas = descartes.GetValueOrDefault("POSTULACION_DUPLICADA");
                var preparadas = descartes.GetValueOrDefault("PREPARACION_DUPLICADA");
                var ocupadas = descartes.GetValueOrDefault("IA_OCUPADA");
                var intentos = descartes.GetValueOrDefault("MaximoIntentos");
                if (duplicadas > 0) razones.Add($"{duplicadas} ya postuladas");
                if (preparadas > 0) razones.Add($"{preparadas} con preparación pendiente");
                if (ocupadas > 0) razones.Add("IA ocupada");
                if (intentos > 0) razones.Add($"{intentos} con máximo de reintentos");
                if (descartes.GetValueOrDefault("OfertaNoDisponibleOExcluida") > 0 ||
                    descartes.GetValueOrDefault("OFERTA_DESCARTADA") > 0)
                    razones.Add("ofertas no disponibles o excluidas");
                var mensaje = candidatos.Count == 0
                    ? $"Sin pendientes elegibles ni recomendaciones disponibles desde {minimoRecomendaciones}%."
                    : razones.Count > 0 ? "Sin nuevas postulaciones: " + string.Join(", ", razones) + "."
                    : "Las ofertas evaluadas no están disponibles para una nueva preparación.";
                await Informar(usuarioId, "SinCandidatos", mensaje, candidatos.Count, ct);
                if (motivos.Length > 0) await registro.RegistrarEvaluacionAsync(usuarioId, motivos, candidatos.Count, ct);
            }
            else if (!falloPreparacion)
                await Informar(usuarioId, "EvaluacionFinalizada", "La evaluación terminó. Se volverá a revisar en el próximo ciclo.", candidatos.Count, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                // Un usuario con errores NO impide procesar a los demás.
                // El error técnico va solo al log, nunca a la respuesta pública.
                diagnostico?.Registrar(usuarioId, "Error",
                    ex is ServicioDependienteException servicio && servicio.Servicio == "Recomendaciones"
                        ? "No se pudieron consultar las recomendaciones. Revisá el servicio de Recomendaciones."
                        : "No se pudo completar la evaluación autónoma.");
                try { await registro.RegistrarFalloUsuarioAsync(usuarioId, ex, CancellationToken.None); }
                catch { /* Un error de auditoría no detiene el ciclo. */ }
            }
        }
        return completadas;
    }

    private async Task Informar(Guid usuarioId, string estado, string mensaje, int? candidatos, CancellationToken ct)
    {
        diagnostico?.Registrar(usuarioId, estado, mensaje, candidatos);
        await registro.RegistrarEvaluacionAsync(usuarioId, estado, candidatos, ct);
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
