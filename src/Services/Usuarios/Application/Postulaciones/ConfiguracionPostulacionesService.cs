using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Auth;

namespace Application.Postulaciones;

public sealed class ConfiguracionPostulacionesService(IUsuarioRepository usuarios)
{
    private const string ClaveConfiguracion = "postulacionesInteligentes";
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public async Task<ConfiguracionPostulaciones?> ObtenerAsync(
        Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var usuario = await usuarios.GetByIdAsync(usuarioId, cancellationToken);
        if (usuario is null || !usuario.Estado) return null;

        var raiz = LeerPreferencias(usuario.PreferenciasJson);
        return LeerConfiguracion(raiz);
    }

    public async Task<ConfiguracionPostulaciones?> GuardarAsync(
        Guid usuarioId,
        ConfiguracionPostulaciones solicitud,
        CancellationToken cancellationToken = default)
    {
        var usuario = await usuarios.GetByIdAsync(usuarioId, cancellationToken);
        if (usuario is null || !usuario.Estado) return null;

        var raiz = LeerPreferencias(usuario.PreferenciasJson);
        var actual = LeerConfiguracion(raiz);
        var normalizada = ValidarYNormalizar(solicitud, actual.Preferencias, actual.OrdenPendientes);

        raiz[ClaveConfiguracion] = JsonSerializer.SerializeToNode(normalizada, OpcionesJson);
        usuario.PreferenciasJson = raiz.ToJsonString();
        await usuarios.UpdateAsync(usuario, cancellationToken);
        return normalizada;
    }

    private static JsonObject LeerPreferencias(string? contenido)
    {
        if (string.IsNullOrWhiteSpace(contenido)) return new JsonObject();
        // PreferenciasJson ya es un objeto JSON en el contrato existente de Usuarios.
        return JsonNode.Parse(contenido) as JsonObject
            ?? throw new JsonException("Las preferencias guardadas deben ser un objeto JSON.");
    }

    private static ConfiguracionPostulaciones LeerConfiguracion(JsonObject raiz)
    {
        if (raiz[ClaveConfiguracion] is null)
            return ConfiguracionPostulaciones.Predeterminada();

        var recuperada = raiz[ClaveConfiguracion]!.Deserialize<ConfiguracionPostulaciones>(OpcionesJson)
            ?? throw new JsonException("La configuracion guardada no es valida.");
        return recuperada with { OrdenPendientes = recuperada.OrdenPendientes ?? "Prioridad",
            ZonaHoraria = string.IsNullOrWhiteSpace(recuperada.ZonaHoraria)
                ? "America/Argentina/Buenos_Aires" : recuperada.ZonaHoraria };
    }

    private static ConfiguracionPostulaciones ValidarYNormalizar(
        ConfiguracionPostulaciones solicitud, PreferenciasLaborales? anteriores,
        string? ordenAnterior)
    {
        if (solicitud is null)
            throw new ValidationException("Debes indicar una configuración de postulaciones.");

        var modo = solicitud.Modo?.Trim();
        if (string.Equals(modo, "Automatica", StringComparison.OrdinalIgnoreCase)) modo = "Automatica";
        else if (string.Equals(modo, "Asistida", StringComparison.OrdinalIgnoreCase)) modo = "Asistida";
        else if (string.Equals(modo, "Desactivada", StringComparison.OrdinalIgnoreCase)) modo = "Desactivada";
        else throw new ValidationException("El modo debe ser Automatica, Asistida o Desactivada.");

        if (solicitud.CompatibilidadMinima is < 1 or > 100)
            throw new ValidationException("La compatibilidad mínima debe estar entre 1 y 100 %.");

        if (solicitud.LimiteDiario is < 1 or > 10)
            throw new ValidationException("El límite diario debe estar entre 1 y 10.");

        // Omitir preferencias en el PUT conserva las preferencias ya guardadas.
        var preferencias = solicitud.Preferencias ?? anteriores ?? PreferenciasLaborales.Vacias();
        if (preferencias.ExpectativaSalarial is < 0)
            throw new ValidationException("La expectativa salarial no puede ser negativa.");

        var normalizadas = new PreferenciasLaborales(
            NormalizarLista(preferencias.PuestosInteres, "puestos de interés"),
            NormalizarLista(preferencias.UbicacionesLaborales, "ubicaciones laborales"),
            NormalizarLista(preferencias.ModalidadesTrabajo, "modalidades de trabajo"),
            NormalizarLista(preferencias.TiposContrato, "tipos de contrato"),
            preferencias.ExpectativaSalarial,
            NormalizarLista(preferencias.Exclusiones, "exclusiones"));

        var orden = (solicitud.OrdenPendientes ?? ordenAnterior ?? "Prioridad").Trim();
        if (string.IsNullOrEmpty(orden)) orden = "Prioridad";
        orden = orden.ToLowerInvariant() switch
        {
            "prioridad" => "Prioridad",
            "compatibilidad" => "Compatibilidad",
            "fechacierre" => "FechaCierre",
            "antiguos" => "Antiguos",
            "recientes" => "Recientes",
            _ => throw new ValidationException("El orden debe ser Prioridad, Compatibilidad, FechaCierre, Antiguos o Recientes.")
        };

        var zona = string.IsNullOrWhiteSpace(solicitud.ZonaHoraria)
            ? "America/Argentina/Buenos_Aires" : solicitud.ZonaHoraria.Trim();
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(zona, out _))
            throw new ValidationException("La zona horaria debe ser una zona IANA válida.");
        return new ConfiguracionPostulaciones(
            modo, solicitud.CompatibilidadMinima, solicitud.LimiteDiario, normalizadas, orden, zona);
    }

    private static List<string> NormalizarLista(List<string>? valores, string nombre)
    {
        var normalizados = new List<string>();
        foreach (var valor in valores ?? [])
        {
            if (string.IsNullOrWhiteSpace(valor))
                throw new ValidationException($"No se permiten valores vacíos en {nombre}.");
            var limpio = valor.Trim();
            if (!normalizados.Contains(limpio, StringComparer.OrdinalIgnoreCase))
                normalizados.Add(limpio);
        }
        return normalizados;
    }
}
