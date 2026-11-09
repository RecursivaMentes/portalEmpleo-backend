using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Application.Chats;
using Application.Perfil;

namespace Application.Cvs;

public record GenerarCvRequest(string? Titulo = null, string? PuestoObjetivo = null, bool EsPrincipal = false);

public enum MotivoErrorGeneracion
{
    /// <summary>Ya hay una generación en curso para esta persona.</summary>
    YaEnCurso,

    /// <summary>No se pudo consultar el perfil en Usuarios.</summary>
    PerfilNoDisponible,

    /// <summary>El perfil no tiene datos suficientes para armar un CV.</summary>
    PerfilIncompleto,

    /// <summary>El modelo respondió, pero el CV no pasó los controles de fidelidad ni siquiera al reintentar.</summary>
    IaNoFiable,

    /// <summary>El CV se generó bien pero Usuarios no lo aceptó (por ejemplo, ya hay demasiados CV).</summary>
    NoSePudoGuardar,
}

/// <param name="Problemas">Qué falló en los controles de fidelidad (solo con <see cref="MotivoErrorGeneracion.IaNoFiable"/>).</param>
/// <param name="TextoGenerado">
/// El CV redactado, cuando se generó bien pero no se pudo guardar: así no se pierde el trabajo (y el costo) de la IA.
/// </param>
public record ResultadoGeneracion(
    CvCreado? Cv,
    MotivoErrorGeneracion? Motivo = null,
    string? Mensaje = null,
    IReadOnlyList<string>? Problemas = null,
    string? TextoGenerado = null)
{
    public bool Exito => Cv is not null;
}

/// <summary>
/// Registro de personas que tienen una generación de CV en curso. Evita que un doble clic (o un cliente que
/// reintenta) dispare dos generaciones a la vez: costarían el doble y dejarían dos CV iguales.
/// Es memoria de un solo proceso; si el servicio corriera en varias instancias habría que usar algo compartido.
/// </summary>
public class GeneracionesEnCurso
{
    private readonly ConcurrentDictionary<Guid, byte> _enCurso = new();

    public bool TryEntrar(Guid usuarioId) => _enCurso.TryAdd(usuarioId, 0);

    public void Salir(Guid usuarioId) => _enCurso.TryRemove(usuarioId, out _);
}

/// <summary>
/// Genera un CV con IA a partir del perfil laboral (RF-11) y lo guarda en Usuarios como un CV nuevo,
/// que después se puede ver, editar y descargar como cualquier otro.
/// </summary>
public partial class GeneracionCvService(
    IAsistenteIA asistente,
    IPerfilUsuarioClient perfiles,
    ICvUsuarioClient cvs,
    GeneracionesEnCurso enCurso)
{
    public const int MaxLargoPuestoObjetivo = 200;

    private const int MinCaracteresCv = 80;
    private const int MaxIntentos = 2;

    /// <exception cref="AsistenteNoDisponibleException">Si el modelo de IA no está disponible.</exception>
    public async Task<ResultadoGeneracion> GenerarAsync(
        Guid usuarioId, string accessToken, GenerarCvRequest request, CancellationToken cancellationToken)
    {
        if (!enCurso.TryEntrar(usuarioId))
            return Error(MotivoErrorGeneracion.YaEnCurso, "Ya estamos generando tu CV. Esperá unos segundos.");

        try
        {
            var perfil = await perfiles.ObtenerAsync(accessToken, cancellationToken);
            if (perfil is null)
                return Error(MotivoErrorGeneracion.PerfilNoDisponible,
                    "No pudimos consultar tu perfil en este momento. Intentá nuevamente más tarde.");

            if (!TieneDatosSuficientes(perfil))
                return Error(MotivoErrorGeneracion.PerfilIncompleto,
                    "Tu perfil laboral todavía no tiene datos suficientes para armar un CV. " +
                    "Cargá tu experiencia, tu formación o tus habilidades (el chatbot te puede ayudar) y volvé a intentar.");

            var puestoObjetivo = LimpiarPuestoObjetivo(request.PuestoObjetivo);
            var datos = PromptGeneracionCv.ConstruirDatos(perfil, puestoObjetivo);

            string? cv = null;
            IReadOnlyList<string> problemas = [];

            for (var intento = 1; intento <= MaxIntentos; intento++)
            {
                var solicitud = PromptGeneracionCv.ConstruirSolicitud(datos, problemas);
                var bruto = await asistente.GenerarTextoAsync(
                    PromptGeneracionCv.Instrucciones, solicitud, cancellationToken);

                var limpio = LimpiarSalida(bruto, perfil);
                problemas = limpio is null
                    ? ["La respuesta no tenía el formato pedido: tiene que ser solo el CV, con las marcas indicadas."]
                    : VerificadorFidelidadCv.Verificar(limpio, perfil, datos);

                if (problemas.Count == 0)
                {
                    cv = limpio;
                    break;
                }
            }

            if (cv is null)
                return new ResultadoGeneracion(null, MotivoErrorGeneracion.IaNoFiable,
                    "La IA no logró armar un CV fiel a tu perfil. Intentá de nuevo.", Problemas: problemas);

            var titulo = string.IsNullOrWhiteSpace(request.Titulo)
                ? TituloPorDefecto(puestoObjetivo)
                : request.Titulo;

            var creado = await cvs.CrearAsync(accessToken, titulo, cv, request.EsPrincipal, cancellationToken);

            return creado.Exito
                ? new ResultadoGeneracion(creado.Cv)
                : new ResultadoGeneracion(null, MotivoErrorGeneracion.NoSePudoGuardar,
                    creado.Mensaje ?? "No se pudo guardar el CV.", TextoGenerado: cv);
        }
        finally
        {
            enCurso.Salir(usuarioId);
        }
    }

    // Hace falta algo de qué escribir: experiencia, formación, habilidades, idiomas o un resumen.
    private static bool TieneDatosSuficientes(PerfilUsuario perfil) =>
        perfil.Laboral is { } l
        && (l.Experiencias.Count > 0 || l.Formacion.Count > 0 || l.Habilidades.Count > 0
            || l.Idiomas.Count > 0 || !string.IsNullOrWhiteSpace(l.Resumen));

    // Es texto que va dentro del prompt: sin saltos de línea ni caracteres de control, y con largo acotado.
    private static string? LimpiarPuestoObjetivo(string? puesto)
    {
        if (string.IsNullOrWhiteSpace(puesto)) return null;

        var limpio = ControlesYSaltos().Replace(puesto, " ").Trim();
        if (limpio.Length > MaxLargoPuestoObjetivo) limpio = limpio[..MaxLargoPuestoObjetivo].TrimEnd();

        return limpio.Length == 0 ? null : limpio;
    }

    private static string TituloPorDefecto(string? puestoObjetivo) =>
        string.IsNullOrWhiteSpace(puestoObjetivo)
            ? $"CV generado con IA ({DateTime.UtcNow:dd/MM/yyyy})"
            : $"CV para {(puestoObjetivo.Length > 150 ? puestoObjetivo[..150] : puestoObjetivo)} (IA)";   // el título admite 200

    /// <summary>
    /// Deja solo el CV: quita cercos de código, comentarios del modelo antes del título y negritas.
    /// Devuelve null si lo que queda es demasiado corto para ser un CV.
    /// </summary>
    internal static string? LimpiarSalida(string? bruto, PerfilUsuario perfil)
    {
        if (string.IsNullOrWhiteSpace(bruto)) return null;

        var texto = bruto.Replace("\r\n", "\n").Replace('\r', '\n');

        // Si el modelo envolvió el CV en un bloque de código, el CV es lo que hay adentro: lo que escribió
        // antes o después ("Aquí tienes", "Espero que te sirva") queda afuera.
        var bloque = BloqueDeCodigo().Match(texto);
        texto = bloque.Success ? bloque.Groups[1].Value : CercoDeCodigo().Replace(texto, string.Empty);

        texto = texto.Replace("**", "").Replace("__", "");

        var lineas = texto.Split('\n').Select(l => l.TrimEnd()).ToList();

        // Si el modelo escribió una introducción ("Aquí tienes tu CV:"), se descarta lo que está antes del título.
        var titulo = lineas.FindIndex(l => l.StartsWith("# ", StringComparison.Ordinal));
        if (titulo > 0) lineas.RemoveRange(0, titulo);

        var resultado = string.Join('\n', lineas).Trim();

        // Si no puso título, se agrega con el nombre real de la persona.
        if (titulo < 0) resultado = $"# {PromptGeneracionCv.NombreCompleto(perfil)}\n{resultado}";

        return resultado.Length >= MinCaracteresCv ? resultado : null;
    }

    private static ResultadoGeneracion Error(MotivoErrorGeneracion motivo, string mensaje) =>
        new(null, motivo, mensaje);

    [GeneratedRegex(@"```[a-zA-Z]*[ \t]*\n(.*?)\n?```", RegexOptions.Singleline)]
    private static partial Regex BloqueDeCodigo();

    // Cerco suelto (abre sin cerrar, o al revés): se quitan solo las líneas con los ```.
    [GeneratedRegex(@"^```[a-zA-Z]*[ \t]*$\n?", RegexOptions.Multiline)]
    private static partial Regex CercoDeCodigo();

    [GeneratedRegex(@"[\p{C}\r\n\t]+")]
    private static partial Regex ControlesYSaltos();
}
