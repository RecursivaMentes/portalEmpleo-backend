using Domain.Entities;
using Domain.Enums;

namespace Application.Postulaciones;

public sealed record OfertaPendienteDatos(
    Guid UsuarioId, Guid EmpleoId, DateTime FechaAgregado,
    string Titulo, DateTime? FechaCierre, string? Ubicacion,
    ModalidadTrabajo Modalidad, TipoContrato TipoContrato, decimal? SalarioMin);

public sealed record PreferenciasParaPrioridad(
    List<string>? PuestosInteres,
    List<string>? UbicacionesLaborales,
    List<string>? ModalidadesTrabajo,
    List<string>? TiposContrato,
    decimal? ExpectativaSalarial);

public sealed record DatosExternosOrdenamiento(
    string OrdenPendientes,
    PreferenciasParaPrioridad? Preferencias,
    IReadOnlyDictionary<Guid, decimal> Compatibilidades);

public sealed record PendienteOrdenadoResponse(
    Guid UsuarioId, Guid EmpleoId, DateTime FechaAgregado,
    string Titulo, DateTime? FechaCierre,
    int Prioridad, decimal? Compatibilidad);

public interface IOrdenamientoPendientesRepository
{
    Task<List<OfertaPendienteDatos>> ObtenerPendientesAsync(Guid usuarioId, CancellationToken ct = default);
}

public interface IDatosExternosOrdenamiento
{
    Task<DatosExternosOrdenamiento> ObtenerAsync(Guid usuarioId, string accessToken, CancellationToken ct = default);
}

public sealed class OrdenamientoPendientesService(
    IOrdenamientoPendientesRepository pendientes,
    IDatosExternosOrdenamiento externos,
    IDatosExternosOrdenamientoInterno? internos = null)
{
    public async Task<List<PendienteOrdenadoResponse>> ListarAsync(
        Guid usuarioId, string accessToken, CancellationToken ct = default)
    {
        if (usuarioId == Guid.Empty || string.IsNullOrWhiteSpace(accessToken))
            throw PostulacionException.DatosInvalidos();
        var ofertas = await pendientes.ObtenerPendientesAsync(usuarioId, ct);
        if (ofertas.Count == 0) return [];
        var datos = await externos.ObtenerAsync(usuarioId, accessToken, ct);
        return Ordenar(ofertas, datos, DateTime.UtcNow);
    }

    public async Task<List<PendienteOrdenadoResponse>> ListarInternoAsync(
        Guid usuarioId, CancellationToken ct = default)
    {
        if (internos is null) throw new InvalidOperationException("Ordenamiento interno no configurado.");
        var ofertas = await pendientes.ObtenerPendientesAsync(usuarioId, ct);
        if (ofertas.Count == 0) return [];
        return Ordenar(ofertas, await internos.ObtenerInternoAsync(usuarioId, ct), DateTime.UtcNow);
    }

    // Un mismo orden para mostrar la lista y para elegir la siguiente oferta a procesar.
    public static List<PendienteOrdenadoResponse> Ordenar(
        IReadOnlyList<OfertaPendienteDatos> ofertas, DatosExternosOrdenamiento datos, DateTime ahora)
    {
        var resultados = ofertas.Select(oferta =>
        {
            var compatibilidad = datos.Compatibilidades.TryGetValue(oferta.EmpleoId, out var score)
                ? Math.Clamp(score, 0m, 100m) : (decimal?)null;
            var preferencias = PuntuarPreferencias(oferta, datos.Preferencias);
            var prioridad = CalcularPrioridad(compatibilidad, oferta.FechaCierre, preferencias,
                oferta.FechaAgregado, ahora);
            return new PendienteOrdenadoResponse(
                oferta.UsuarioId, oferta.EmpleoId, oferta.FechaAgregado,
                oferta.Titulo, oferta.FechaCierre, prioridad, compatibilidad);
        }).ToList();

        // Desempates entre criterios funcionales; sin usar el Id como prioridad.
        var orden = datos.OrdenPendientes;
        IOrderedEnumerable<PendienteOrdenadoResponse> ordenados = orden switch
        {
            "Compatibilidad" => resultados
                .OrderByDescending(x => x.Compatibilidad ?? -1m)
                .ThenByDescending(x => x.Prioridad)
                .ThenBy(x => x.FechaCierre ?? DateTime.MaxValue)
                .ThenBy(x => x.FechaAgregado),
            "FechaCierre" => resultados
                .OrderBy(x => x.FechaCierre.HasValue ? 0 : 1)
                .ThenBy(x => x.FechaCierre ?? DateTime.MaxValue)
                .ThenByDescending(x => x.Prioridad)
                .ThenByDescending(x => x.Compatibilidad ?? -1m)
                .ThenBy(x => x.FechaAgregado),
            "Antiguos" => resultados
                .OrderBy(x => x.FechaAgregado)
                .ThenByDescending(x => x.Prioridad)
                .ThenByDescending(x => x.Compatibilidad ?? -1m)
                .ThenBy(x => x.FechaCierre ?? DateTime.MaxValue),
            "Recientes" => resultados
                .OrderByDescending(x => x.FechaAgregado)
                .ThenByDescending(x => x.Prioridad)
                .ThenByDescending(x => x.Compatibilidad ?? -1m)
                .ThenBy(x => x.FechaCierre ?? DateTime.MaxValue),
            _ => resultados
                .OrderByDescending(x => x.Prioridad)
                .ThenByDescending(x => x.Compatibilidad ?? -1m)
                .ThenBy(x => x.FechaCierre ?? DateTime.MaxValue)
                .ThenBy(x => x.FechaAgregado)
        };
        return ordenados.ThenBy(x => x.Titulo, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // Los factores que carecen de datos no reciben puntajes inventados:
    // los pesos disponibles se normalizan para totalizar 100 %.
    public static int CalcularPrioridad(decimal? compatibilidad, DateTime? cierre,
        decimal? preferencias, DateTime fechaAgregado, DateTime ahora)
    {
        decimal puntos = 0m;
        decimal pesos = 0m;
        if (compatibilidad.HasValue)
        {
            puntos += Math.Clamp(compatibilidad.Value, 0m, 100m) * 55m;
            pesos += 55m;
        }
        if (cierre.HasValue)
        {
            var diasRestantes = (decimal)(cierre.Value - ahora).TotalDays;
            // Mayor urgencia cuanto menor sea el tiempo restante (horizonte de 30 dias).
            var urgencia = 100m * (1m - Math.Clamp(diasRestantes / 30m, 0m, 1m));
            puntos += urgencia * 25m;
            pesos += 25m;
        }
        if (preferencias.HasValue)
        {
            puntos += Math.Clamp(preferencias.Value, 0m, 100m) * 15m;
            pesos += 15m;
        }
        // Antiguedad: alcanza el maximo a los 30 dias; nunca negativa.
        var antiguedad = 100m * Math.Clamp((decimal)(ahora - fechaAgregado).TotalDays / 30m, 0m, 1m);
        puntos += antiguedad * 5m;
        pesos += 5m;
        return pesos == 0m ? 0 : (int)Math.Round(puntos / pesos, 0, MidpointRounding.AwayFromZero);
    }

    private static decimal? PuntuarPreferencias(OfertaPendienteDatos oferta, PreferenciasParaPrioridad? pref)
    {
        if (pref is null) return null;
        var total = 0;
        var coincidencias = 0;
        if (pref.PuestosInteres is { Count: > 0 })
        {
            total++;
            if (pref.PuestosInteres.Any(x => !string.IsNullOrWhiteSpace(x) &&
                oferta.Titulo.Contains(x, StringComparison.OrdinalIgnoreCase))) coincidencias++;
        }
        if (pref.UbicacionesLaborales is { Count: > 0 })
        {
            total++;
            if (pref.UbicacionesLaborales.Any(x => !string.IsNullOrWhiteSpace(x) &&
                oferta.Ubicacion?.Contains(x, StringComparison.OrdinalIgnoreCase) == true)) coincidencias++;
        }
        if (pref.ModalidadesTrabajo is { Count: > 0 })
        {
            total++;
            if (pref.ModalidadesTrabajo.Any(x =>
                string.Equals(x?.Trim(), oferta.Modalidad.ToString(), StringComparison.OrdinalIgnoreCase))) coincidencias++;
        }
        if (pref.TiposContrato is { Count: > 0 })
        {
            total++;
            if (pref.TiposContrato.Any(x =>
                string.Equals(x?.Trim(), oferta.TipoContrato.ToString(), StringComparison.OrdinalIgnoreCase))) coincidencias++;
        }
        if (pref.ExpectativaSalarial.HasValue && oferta.SalarioMin.HasValue)
        {
            total++;
            if (oferta.SalarioMin.Value >= pref.ExpectativaSalarial.Value) coincidencias++;
        }
        return total == 0 ? null : 100m * coincidencias / total;
    }
}
