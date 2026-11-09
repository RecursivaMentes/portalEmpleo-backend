using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Perfil;

namespace Application.Cvs;

/// <summary>Lo que quedó de la respuesta de la IA después de validarla, todavía sin compararlo con el perfil actual.</summary>
public record PropuestaCruda(
    string? Resumen,
    string? Telefono,
    IReadOnlyList<ExperienciaUsuario> Experiencias,
    IReadOnlyList<FormacionUsuario> Formacion,
    IReadOnlyList<HabilidadUsuario> Habilidades,
    IReadOnlyList<IdiomaUsuario> Idiomas,
    IReadOnlyList<ItemIncompleto> Incompletos);

/// <summary>
/// Convierte la respuesta JSON de la IA en datos de perfil válidos. Se interpreta de forma tolerante (la IA puede
/// rodear el JSON de texto o equivocar tipos) y cada dato se valida con las reglas con las que Usuarios guarda el
/// perfil. Lo que el CV menciona pero no se puede guardar por faltarle un dato obligatorio (por ejemplo, una
/// experiencia sin fecha de inicio) queda como "incompleto" para preguntárselo a la persona.
/// La propuesta nunca se guarda sola: la persona la revisa antes de aplicarla.
/// </summary>
public static class LectorPropuestaIA
{
    private const int MaxItems = 30;
    private const int MaxHabilidades = 40;
    private const int MaxIdiomas = 10;

    private static readonly string[] NivelesEducativos =
        ["Primaria", "Secundaria", "Terciaria", "Universitaria de grado", "Posgrado", "Curso o certificación"];

    private static readonly string[] NivelesIdioma = ["A1", "A2", "B1", "B2", "C1", "C2", "Nativo"];

    private static readonly string[] ValoresVacios =
        ["null", "n/a", "na", "-", "ninguno", "ninguna", "no especificado", "no indica", "no figura", "desconocido"];

    private static readonly string[] FormatosFecha = ["yyyy-MM-dd", "yyyy-MM", "yyyy", "dd/MM/yyyy", "MM/yyyy"];

    /// <summary>Devuelve null si la respuesta no trae un objeto JSON utilizable (conviene reintentar).</summary>
    public static PropuestaCruda? Interpretar(string? respuesta, DateOnly hoy)
    {
        var json = ExtraerObjetoJson(respuesta);
        if (json is null) return null;

        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
        }
        catch (JsonException)
        {
            return null;
        }

        using (documento)
        {
            var raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object || !TieneAlgunaClave(raiz)) return null;

            var incompletos = new List<ItemIncompleto>();

            return new PropuestaCruda(
                Largo(Cadena(raiz, "resumen"), 2000),
                Largo(Cadena(raiz, "telefono"), 30),
                LeerExperiencias(raiz, hoy, incompletos),
                LeerFormaciones(raiz, hoy, incompletos),
                LeerHabilidades(raiz),
                LeerIdiomas(raiz, incompletos),
                incompletos);
        }
    }

    private static List<ExperienciaUsuario> LeerExperiencias(JsonElement raiz, DateOnly hoy, List<ItemIncompleto> incompletos)
    {
        var resultado = new List<ExperienciaUsuario>();

        foreach (var e in Objetos(raiz, "experiencias").Take(MaxItems))
        {
            var empresa = Cadena(e, "empresa");
            var puesto = Cadena(e, "puesto");
            if (empresa is null || puesto is null || empresa.Length > 150 || puesto.Length > 120) continue;

            var esActual = Booleano(e, "esActual");
            var inicio = Fecha(e, "fechaInicio", hoy);
            var fin = esActual ? null : Fecha(e, "fechaFin", hoy);
            if (fin < inicio) fin = null;

            if (inicio is null)
            {
                incompletos.Add(new ItemIncompleto("experiencia", $"{puesto} en {empresa}", ["fecha de inicio"]));
                continue;
            }

            resultado.Add(new ExperienciaUsuario(
                empresa, puesto, Largo(Cadena(e, "ubicacion"), 120), Largo(Cadena(e, "descripcion"), 2000),
                Iso(inicio), Iso(fin), esActual));
        }

        return resultado;
    }

    private static List<FormacionUsuario> LeerFormaciones(JsonElement raiz, DateOnly hoy, List<ItemIncompleto> incompletos)
    {
        var resultado = new List<FormacionUsuario>();

        foreach (var f in Objetos(raiz, "formacion").Take(MaxItems))
        {
            var institucion = Cadena(f, "institucion");
            if (institucion is null || institucion.Length > 150) continue;

            var titulo = Largo(Cadena(f, "titulo"), 150);
            var nivel = MapearNivelEducativo(Cadena(f, "nivelEducativo"));
            var estado = MapearEstado(Cadena(f, "estado"));
            var inicio = Fecha(f, "fechaInicio", hoy);
            var fin = Fecha(f, "fechaFin", hoy);
            if (fin < inicio) fin = null;

            // Lo que Usuarios exige para guardar una formación; lo que falte se le pregunta a la persona.
            var faltan = new List<string>();
            if (nivel is null) faltan.Add("nivel educativo");
            if (estado is null) faltan.Add("estado (en curso, finalizado o incompleto)");
            if (titulo is null && nivel is not ("Primaria" or "Secundaria")) faltan.Add("título");
            if (estado == "Finalizado" && fin is null) faltan.Add("fecha de finalización");

            if (faltan.Count > 0)
            {
                incompletos.Add(new ItemIncompleto(
                    "formacion", titulo is null ? institucion : $"{titulo} en {institucion}", faltan));
                continue;
            }

            resultado.Add(new FormacionUsuario(
                institucion, titulo, nivel!, estado!, Iso(inicio), Iso(fin), Largo(Cadena(f, "descripcion"), 2000)));
        }

        return resultado;
    }

    // El nivel de una habilidad no se importa: es opcional y una IA no puede saberlo con certeza.
    private static List<HabilidadUsuario> LeerHabilidades(JsonElement raiz) =>
        Objetos(raiz, "habilidades")
            .Select(h => Cadena(h, "nombre"))
            .Where(n => n is { Length: <= 100 })
            .DistinctBy(n => TextoComparable.Normalizar(n))
            .Take(MaxHabilidades)
            .Select(n => new HabilidadUsuario(n!, null))
            .ToList();

    private static List<IdiomaUsuario> LeerIdiomas(JsonElement raiz, List<ItemIncompleto> incompletos)
    {
        var resultado = new List<IdiomaUsuario>();
        var vistos = new HashSet<string>();

        foreach (var i in Objetos(raiz, "idiomas"))
        {
            var idioma = Cadena(i, "idioma");
            if (idioma is null || idioma.Length > 100 || !vistos.Add(TextoComparable.Normalizar(idioma))) continue;
            if (resultado.Count + incompletos.Count(x => x.Tipo == "idioma") >= MaxIdiomas) break;

            // El nivel es obligatorio en el perfil: si no hay uno válido, se pregunta.
            var nivel = NivelesIdioma.FirstOrDefault(n =>
                string.Equals(n, Cadena(i, "nivel"), StringComparison.OrdinalIgnoreCase));

            if (nivel is null) incompletos.Add(new ItemIncompleto("idioma", idioma, ["nivel"]));
            else resultado.Add(new IdiomaUsuario(idioma, nivel));
        }

        return resultado;
    }

    private static string? MapearNivelEducativo(string? texto)
    {
        if (texto is null) return null;

        var t = TextoComparable.Normalizar(texto);
        var exacto = NivelesEducativos.FirstOrDefault(n => TextoComparable.Normalizar(n) == t);
        if (exacto is not null) return exacto;

        if (Regex.IsMatch(t, "posgrado|maestr|doctor|especializ")) return "Posgrado";
        if (Regex.IsMatch(t, "universit|grado|licenciatur|ingenier")) return "Universitaria de grado";
        if (Regex.IsMatch(t, "terciar|tecnicatur")) return "Terciaria";
        if (Regex.IsMatch(t, "secundar|bachiller")) return "Secundaria";
        if (Regex.IsMatch(t, "primar")) return "Primaria";
        if (Regex.IsMatch(t, "curso|certific|bootcamp")) return "Curso o certificación";
        return null;
    }

    private static string? MapearEstado(string? texto)
    {
        var t = TextoComparable.Normalizar(texto).Replace(" ", "");
        if (t is "encurso" or "cursando") return "EnCurso";
        if (t is "finalizado" or "completo" or "terminado" or "graduado" or "egresado") return "Finalizado";
        if (t is "incompleto" or "abandonado" or "trunco") return "Incompleto";
        return null;
    }

    // ---- lectura tolerante del JSON

    /// <summary>Busca el primer objeto JSON completo dentro del texto (por si el modelo agregó algo alrededor).</summary>
    internal static string? ExtraerObjetoJson(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;

        var inicio = texto.IndexOf('{');
        if (inicio < 0) return null;

        var profundidad = 0;
        var enTexto = false;
        var escapado = false;

        for (var i = inicio; i < texto.Length; i++)
        {
            var c = texto[i];

            if (enTexto)
            {
                if (escapado) escapado = false;
                else if (c == '\\') escapado = true;
                else if (c == '"') enTexto = false;
            }
            else if (c == '"') enTexto = true;
            else if (c == '{') profundidad++;
            else if (c == '}' && --profundidad == 0) return texto[inicio..(i + 1)];
        }

        return null;
    }

    private static bool TieneAlgunaClave(JsonElement raiz) =>
        raiz.EnumerateObject().Any(p => p.Name.ToLowerInvariant() is
            "resumen" or "telefono" or "experiencias" or "formacion" or "habilidades" or "idiomas");

    private static IEnumerable<JsonElement> Objetos(JsonElement raiz, string nombre) =>
        raiz.EnumerateObject()
            .Where(p => p.Name.Equals(nombre, StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.Array)
            .SelectMany(p => p.Value.EnumerateArray())
            .Where(e => e.ValueKind == JsonValueKind.Object);

    private static string? Cadena(JsonElement objeto, string nombre)
    {
        foreach (var p in objeto.EnumerateObject())
        {
            if (!p.Name.Equals(nombre, StringComparison.OrdinalIgnoreCase)) continue;

            var valor = (p.Value.ValueKind switch
            {
                JsonValueKind.String => p.Value.GetString(),
                JsonValueKind.Number => p.Value.GetRawText(),
                _ => null,
            })?.Trim();

            return string.IsNullOrWhiteSpace(valor) || ValoresVacios.Contains(valor.ToLowerInvariant()) ? null : valor;
        }

        return null;
    }

    private static bool Booleano(JsonElement objeto, string nombre) =>
        objeto.EnumerateObject().Any(p => p.Name.Equals(nombre, StringComparison.OrdinalIgnoreCase)
            && (p.Value.ValueKind == JsonValueKind.True
                || (p.Value.ValueKind == JsonValueKind.String
                    && p.Value.GetString()?.Trim().ToLowerInvariant() is "true" or "si" or "sí")));

    // Fecha real y razonable, con el mismo criterio de Usuarios: ni futura ni de hace más de 100 años.
    private static DateOnly? Fecha(JsonElement objeto, string nombre, DateOnly hoy)
    {
        var texto = Cadena(objeto, nombre);
        if (texto is null
            || !DateOnly.TryParseExact(texto, FormatosFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
            return null;

        return fecha > hoy || fecha < hoy.AddYears(-100) ? null : fecha;
    }

    private static string? Largo(string? texto, int maximo) =>
        texto is null || texto.Length <= maximo ? texto : texto[..maximo].TrimEnd();

    private static string? Iso(DateOnly? fecha) => fecha?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
