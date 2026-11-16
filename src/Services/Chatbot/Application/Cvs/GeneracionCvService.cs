using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Application.Chats;
using Application.Perfil;

namespace Application.Cvs;

public record GenerarCvRequest(string? Titulo = null, string? PuestoObjetivo = null, bool EsPrincipal = false);

public enum MotivoErrorGeneracion
{
    YaEnCurso,
    PerfilNoDisponible,
    PerfilIncompleto,

    /// <summary>El modelo respondió algo que no sirve como CV (vacío o demasiado corto).</summary>
    IaNoFiable,

    /// <summary>El CV se generó bien pero Usuarios no lo aceptó (por ejemplo, ya hay demasiados CV).</summary>
    NoSePudoGuardar,
}

/// <param name="TextoGenerado">
/// El CV redactado cuando no se pudo guardar, para no perder el trabajo (y el costo) de la IA.
/// </param>
public record ResultadoGeneracion(
    CvUsuario? Cv,
    MotivoErrorGeneracion? Motivo = null,
    string? Mensaje = null,
    string? TextoGenerado = null)
{
    public bool Exito => Cv is not null;
}

/// <summary>
/// Personas con una operación de IA o de escritura del perfil en curso. Evita que un doble clic dispare dos a la vez:
/// costarían el doble y, como Usuarios reemplaza el perfil entero al guardar, una pisaría a la otra.
/// Vive en la memoria de un solo proceso.
/// </summary>
public class GeneracionesEnCurso
{
    public const string MensajeYaEnCurso = "Ya estamos procesando otra solicitud tuya. Esperá unos segundos.";

    private readonly ConcurrentDictionary<Guid, byte> _enCurso = new();

    public bool TryEntrar(Guid usuarioId) => _enCurso.TryAdd(usuarioId, 0);

    public void Salir(Guid usuarioId) => _enCurso.TryRemove(usuarioId, out _);
}

/// <summary>
/// Genera un CV con IA a partir del perfil laboral (RF-11) y lo guarda en Usuarios como un CV nuevo.
/// </summary>
public partial class GeneracionCvService(
    IAsistenteIA asistente,
    IPerfilUsuarioClient perfiles,
    ICvUsuarioClient cvs,
    GeneracionesEnCurso enCurso)
{
    public const int MaxLargoPuestoObjetivo = 200;

    private const int MinCaracteresCv = 80;

    /// <exception cref="AsistenteNoDisponibleException">Si el modelo de IA no está disponible.</exception>
    public async Task<ResultadoGeneracion> GenerarAsync(
        Guid usuarioId, string accessToken, GenerarCvRequest request, CancellationToken cancellationToken)
    {
        if (!enCurso.TryEntrar(usuarioId))
            return Error(MotivoErrorGeneracion.YaEnCurso, GeneracionesEnCurso.MensajeYaEnCurso);

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

            var bruto = await asistente.GenerarTextoAsync(
                PromptGeneracionCv.Instrucciones,
                PromptGeneracionCv.ConstruirSolicitud(PromptGeneracionCv.ConstruirDatos(perfil, puestoObjetivo)),
                cancellationToken);

            var cv = LimpiarSalida(bruto, perfil);
            if (cv is null)
                return Error(MotivoErrorGeneracion.IaNoFiable, "La IA no devolvió un CV utilizable. Intentá de nuevo.");

            var titulo = string.IsNullOrWhiteSpace(request.Titulo) ? TituloPorDefecto(puestoObjetivo) : request.Titulo;

            var creado = await cvs.CrearAsync(accessToken, titulo, cv, request.EsPrincipal, cancellationToken);

            return creado.Exito
                ? new ResultadoGeneracion(creado.Cv)
                : new ResultadoGeneracion(null, MotivoErrorGeneracion.NoSePudoGuardar,
                    creado.Mensaje ?? "No se pudo guardar el CV.", cv);
        }
        finally
        {
            enCurso.Salir(usuarioId);
        }
    }

    private static bool TieneDatosSuficientes(PerfilUsuario perfil) =>
        perfil.Laboral is { } l
        && (l.Experiencias.Count > 0 || l.Formacion.Count > 0 || l.Habilidades.Count > 0
            || l.Idiomas.Count > 0 || !string.IsNullOrWhiteSpace(l.Resumen));

    // Va dentro del prompt: una sola línea, sin caracteres de control y con largo acotado.
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
    /// Deja solo el CV: quita cercos de código, introducciones, cierres y negritas, y agrega el título si faltaba.
    /// Devuelve null si lo que queda es demasiado corto para ser un CV.
    /// </summary>
    internal static string? LimpiarSalida(string? bruto, PerfilUsuario perfil)
    {
        if (string.IsNullOrWhiteSpace(bruto)) return null;

        var texto = bruto.Replace("\r\n", "\n").Replace('\r', '\n');

        // Con bloque de código, el CV es lo de adentro; lo escrito antes o después queda afuera.
        var bloque = BloqueDeCodigo().Match(texto);
        texto = bloque.Success ? bloque.Groups[1].Value : CercoDeCodigo().Replace(texto, string.Empty);
        texto = texto.Replace("**", "").Replace("__", "");

        var lineas = texto.Split('\n').Select(l => l.TrimEnd()).ToList();

        var titulo = lineas.FindIndex(l => l.StartsWith("# ", StringComparison.Ordinal));
        if (titulo > 0) lineas.RemoveRange(0, titulo);   // introducción antes del título

        var resultado = string.Join('\n', lineas).Trim();
        if (titulo < 0) resultado = $"# {PromptGeneracionCv.NombreCompleto(perfil)}\n{resultado}";

        return resultado.Length >= MinCaracteresCv ? resultado : null;
    }

    private static ResultadoGeneracion Error(MotivoErrorGeneracion motivo, string mensaje) =>
        new(null, motivo, mensaje);

    [GeneratedRegex(@"```[a-zA-Z]*[ \t]*\n(.*?)\n?```", RegexOptions.Singleline)]
    private static partial Regex BloqueDeCodigo();

    [GeneratedRegex(@"^```[a-zA-Z]*[ \t]*$\n?", RegexOptions.Multiline)]
    private static partial Regex CercoDeCodigo();

    [GeneratedRegex(@"[\p{C}\r\n\t]+")]
    private static partial Regex ControlesYSaltos();
}
