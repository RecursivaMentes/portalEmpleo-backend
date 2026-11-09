using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Perfil;

namespace Application.Cvs;

/// <summary>Lo que quedó de la respuesta de la IA después de validarla. Todavía sin compararlo con el perfil actual.</summary>
public record PropuestaCruda(
    string? Resumen,
    string? Telefono,
    IReadOnlyList<ExperienciaUsuario> Experiencias,
    IReadOnlyList<FormacionUsuario> Formacion,
    IReadOnlyList<HabilidadUsuario> Habilidades,
    IReadOnlyList<IdiomaUsuario> Idiomas,
    IReadOnlyList<ItemIncompleto> Incompletos,
    int Descartados);

/// <summary>
/// Convierte la respuesta JSON de la IA en datos de perfil confiables.
///
/// Una IA puede devolver JSON mal formado, campos con tipos equivocados, valores fuera de las listas permitidas
/// o directamente datos inventados. Por eso nada de lo que dice se acepta sin comprobar:
///  - se interpreta el JSON de forma tolerante (no se exige un esquema exacto);
///  - cada dato se valida con las mismas reglas que aplica Usuarios al guardar el perfil;
///  - cada dato se "ancla" al texto real del CV: si una empresa, institución, habilidad o idioma no aparece
///    escrito en el CV, se descarta; los textos libres (descripciones, resumen) deben estar mayormente copiados;
///  - lo que está en el CV pero no se puede importar por faltarle un dato obligatorio (por ejemplo, una
///    experiencia sin fecha de inicio) no se pierde: queda como "incompleto" para preguntárselo a la persona.
/// </summary>
public static class LectorPropuestaIA
{
    private const double MinProporcionTextoLibre = 0.6;
    private const double MinProporcionPuesto = 0.5;
    private const int MaxExperiencias = 30;
    private const int MaxFormaciones = 30;
    private const int MaxHabilidades = 40;
    private const int MaxIdiomas = 10;

    private static readonly string[] NivelesEducativos =
        ["Primaria", "Secundaria", "Terciaria", "Universitaria de grado", "Posgrado", "Curso o certificación"];

    private static readonly string[] NivelesIdioma = ["A1", "A2", "B1", "B2", "C1", "C2", "Nativo"];

    private static readonly string[] ValoresVacios =
        ["null", "n/a", "na", "-", "ninguno", "ninguna", "no especificado", "no indica", "no figura", "desconocido"];

    private static readonly string[] FormatosFecha =
        ["yyyy-MM-dd", "yyyy-MM", "yyyy", "dd/MM/yyyy", "MM/yyyy"];

    /// <summary>Devuelve null si la respuesta no contiene un objeto JSON utilizable (conviene reintentar).</summary>
    public static PropuestaCruda? Interpretar(string? respuesta, string textoCv, DateOnly hoy)
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

            var ctx = new Contexto(textoCv, hoy);
            var incompletos = new List<ItemIncompleto>();

            var experiencias = LeerExperiencias(raiz, ctx, incompletos);
            var formacion = LeerFormaciones(raiz, ctx, incompletos);
            var habilidades = LeerHabilidades(raiz, ctx);
            var idiomas = LeerIdiomas(raiz, ctx, incompletos);

            return new PropuestaCruda(
                TextoLibre(Cadena(raiz, "resumen"), ctx),
                LeerTelefono(Cadena(raiz, "telefono"), ctx),
                experiencias, formacion, habilidades, idiomas, incompletos, ctx.Descartados);
        }
    }

    // ------------------------------------------------------------------ secciones

    private static List<ExperienciaUsuario> LeerExperiencias(JsonElement raiz, Contexto ctx, List<ItemIncompleto> incompletos)
    {
        var resultado = new List<ExperienciaUsuario>();

        foreach (var e in Objetos(raiz, "experiencias").Take(MaxExperiencias))
        {
            var empresa = Cadena(e, "empresa");
            var puesto = Cadena(e, "puesto");

            // Sin empresa o puesto, o con una empresa que el CV no menciona, no hay nada que preguntar: se descarta.
            if (empresa is null || puesto is null || empresa.Length > 150 || puesto.Length > 120
                || !ctx.Figura(empresa) || TextoComparable.ProporcionPresente(puesto, ctx.Palabras) < MinProporcionPuesto)
            {
                ctx.Descartados++;
                continue;
            }

            var ubicacion = Cadena(e, "ubicacion");
            if (ubicacion is not null && (ubicacion.Length > 120 || !ctx.Figura(ubicacion))) ubicacion = null;

            var esActual = Booleano(e, "esActual");
            var inicio = Fecha(e, "fechaInicio", ctx);
            var fin = esActual ? null : Fecha(e, "fechaFin", ctx);
            if (fin is not null && inicio is not null && fin < inicio) fin = null;

            if (inicio is null)
            {
                incompletos.Add(new ItemIncompleto("experiencia", $"{puesto} en {empresa}", ["fecha de inicio"]));
                continue;
            }

            resultado.Add(new ExperienciaUsuario(
                empresa, puesto, ubicacion, TextoLibre(Cadena(e, "descripcion"), ctx),
                Iso(inicio), Iso(fin), esActual));
        }

        return resultado;
    }

    private static List<FormacionUsuario> LeerFormaciones(JsonElement raiz, Contexto ctx, List<ItemIncompleto> incompletos)
    {
        var resultado = new List<FormacionUsuario>();

        foreach (var f in Objetos(raiz, "formacion").Take(MaxFormaciones))
        {
            var institucion = Cadena(f, "institucion");
            if (institucion is null || institucion.Length > 150 || !ctx.Figura(institucion))
            {
                ctx.Descartados++;
                continue;
            }

            var titulo = Cadena(f, "titulo");
            if (titulo is not null && (titulo.Length > 150 || TextoComparable.ProporcionPresente(titulo, ctx.Palabras) < MinProporcionPuesto))
                titulo = null;

            var nivel = MapearNivelEducativo(Cadena(f, "nivelEducativo"));
            var estado = MapearEstado(Cadena(f, "estado"));
            var inicio = Fecha(f, "fechaInicio", ctx);
            var fin = Fecha(f, "fechaFin", ctx);
            if (fin is not null && inicio is not null && fin < inicio) fin = null;

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
                institucion, titulo, nivel!, estado!, Iso(inicio), Iso(fin), TextoLibre(Cadena(f, "descripcion"), ctx)));
        }

        return resultado;
    }

    private static List<HabilidadUsuario> LeerHabilidades(JsonElement raiz, Contexto ctx)
    {
        var resultado = new List<HabilidadUsuario>();
        var vistos = new HashSet<string>();

        foreach (var h in Objetos(raiz, "habilidades"))
        {
            if (resultado.Count >= MaxHabilidades) break;

            var nombre = Cadena(h, "nombre");
            if (nombre is null || nombre.Length > 100) continue;

            if (!ctx.Figura(nombre))
            {
                ctx.Descartados++;
                continue;
            }

            if (!vistos.Add(TextoComparable.Normalizar(nombre))) continue;

            // El nivel solo se acepta si el CV realmente lo dice, junto a esa habilidad: la IA no puede "calcularlo".
            var original = Cadena(h, "nivelOriginal");
            var nivel = original is not null && ctx.FiguraCerca(nombre, original) ? MapearNivelHabilidad(original) : null;

            resultado.Add(new HabilidadUsuario(nombre, nivel));
        }

        return resultado;
    }

    private static List<IdiomaUsuario> LeerIdiomas(JsonElement raiz, Contexto ctx, List<ItemIncompleto> incompletos)
    {
        var resultado = new List<IdiomaUsuario>();
        var vistos = new HashSet<string>();

        foreach (var i in Objetos(raiz, "idiomas"))
        {
            if (resultado.Count >= MaxIdiomas) break;

            var idioma = Cadena(i, "idioma");
            if (idioma is null || idioma.Length > 100) continue;

            if (!ctx.Figura(idioma))
            {
                ctx.Descartados++;
                continue;
            }

            if (!vistos.Add(TextoComparable.Normalizar(idioma))) continue;

            // El nivel de un idioma es obligatorio en el perfil: solo vale si el CV lo dice de verdad.
            var original = Cadena(i, "nivelOriginal");
            var nivel = NivelesIdioma.FirstOrDefault(n =>
                string.Equals(n, Cadena(i, "nivel"), StringComparison.OrdinalIgnoreCase));

            if (original is null || !ctx.FiguraCerca(idioma, original) || nivel is null)
            {
                incompletos.Add(new ItemIncompleto("idioma", idioma, ["nivel"]));
                continue;
            }

            resultado.Add(new IdiomaUsuario(idioma, nivel));
        }

        return resultado;
    }

    private static string? LeerTelefono(string? telefono, Contexto ctx)
    {
        if (telefono is null || telefono.Length > 30) return null;

        // El teléfono tiene que estar en el CV (se comparan solo los dígitos, sin guiones ni espacios).
        var digitos = SoloDigitos(telefono);
        return digitos.Length >= 6 && ctx.Digitos.Contains(digitos, StringComparison.Ordinal) ? telefono : null;
    }

    // Un texto libre (descripción, resumen) tiene que estar mayormente copiado del CV; si no, se descarta.
    private static string? TextoLibre(string? texto, Contexto ctx)
    {
        if (texto is null) return null;
        if (TextoComparable.ProporcionPresente(texto, ctx.Palabras) < MinProporcionTextoLibre) return null;

        return texto.Length > 2000 ? texto[..2000].TrimEnd() : texto;
    }

    // ------------------------------------------------------------------ equivalencias de valores

    private static string? MapearNivelEducativo(string? texto)
    {
        if (texto is null) return null;

        var exacto = NivelesEducativos.FirstOrDefault(n =>
            TextoComparable.Normalizar(n) == TextoComparable.Normalizar(texto));
        if (exacto is not null) return exacto;

        var t = TextoComparable.Normalizar(texto);
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
        if (texto is null) return null;

        var t = TextoComparable.Normalizar(texto).Replace(" ", "");
        if (t is "encurso" or "cursando") return "EnCurso";
        if (t is "finalizado" or "completo" or "terminado" or "graduado" or "egresado") return "Finalizado";
        if (t is "incompleto" or "abandonado" or "trunco") return "Incompleto";
        return null;
    }

    private static string? MapearNivelHabilidad(string texto)
    {
        var t = TextoComparable.Normalizar(texto);
        if (Regex.IsMatch(t, "avanz|advanc|expert")) return "Avanzado";
        if (Regex.IsMatch(t, "intermed")) return "Intermedio";
        if (Regex.IsMatch(t, "basic|inicial|beginner")) return "Basico";
        return null;
    }

    // ------------------------------------------------------------------ lectura tolerante del JSON

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
                continue;
            }

            if (c == '"') enTexto = true;
            else if (c == '{') profundidad++;
            else if (c == '}' && --profundidad == 0) return texto[inicio..(i + 1)];
        }

        return null;
    }

    private static bool TieneAlgunaClave(JsonElement raiz) =>
        raiz.EnumerateObject().Any(p => p.Name.ToLowerInvariant() is
            "resumen" or "telefono" or "experiencias" or "formacion" or "habilidades" or "idiomas");

    private static IEnumerable<JsonElement> Objetos(JsonElement raiz, string nombre)
    {
        foreach (var p in raiz.EnumerateObject())
        {
            if (!p.Name.Equals(nombre, StringComparison.OrdinalIgnoreCase) || p.Value.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var elemento in p.Value.EnumerateArray())
                if (elemento.ValueKind == JsonValueKind.Object) yield return elemento;
        }
    }

    private static string? Cadena(JsonElement objeto, string nombre)
    {
        foreach (var p in objeto.EnumerateObject())
        {
            if (!p.Name.Equals(nombre, StringComparison.OrdinalIgnoreCase)) continue;

            var crudo = p.Value.ValueKind switch
            {
                JsonValueKind.String => p.Value.GetString(),
                JsonValueKind.Number => p.Value.GetRawText(),
                _ => null,
            };
            var valor = crudo?.Trim();

            return string.IsNullOrWhiteSpace(valor) || ValoresVacios.Contains(valor.ToLowerInvariant()) ? null : valor;
        }

        return null;
    }

    private static bool Booleano(JsonElement objeto, string nombre)
    {
        foreach (var p in objeto.EnumerateObject())
        {
            if (!p.Name.Equals(nombre, StringComparison.OrdinalIgnoreCase)) continue;

            return p.Value.ValueKind == JsonValueKind.True
                   || (p.Value.ValueKind == JsonValueKind.String
                       && p.Value.GetString()?.Trim().ToLowerInvariant() is "true" or "si" or "sí");
        }

        return false;
    }

    // La fecha tiene que ser real, razonable (no futura ni de hace más de 100 años, como exige Usuarios) y su
    // año tiene que figurar en el CV: si el modelo "completó" una fecha que el CV no dice, se la ignora.
    private static DateOnly? Fecha(JsonElement objeto, string nombre, Contexto ctx)
    {
        var texto = Cadena(objeto, nombre);
        if (texto is null) return null;

        if (!DateOnly.TryParseExact(texto, FormatosFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
            return null;

        if (fecha > ctx.Hoy || fecha < ctx.Hoy.AddYears(-100)) return null;

        return ctx.AniosEnCv.Contains(fecha.Year.ToString(CultureInfo.InvariantCulture)) ? fecha : null;
    }

    private static string? Iso(DateOnly? fecha) => fecha?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string SoloDigitos(string texto) => new(texto.Where(char.IsDigit).ToArray());

    /// <summary>El texto del CV ya preparado para compararlo, y el contador de datos descartados.</summary>
    private sealed class Contexto
    {
        private readonly string _textoNormalizado;

        // Igual que el anterior pero conservando los saltos de línea (se normaliza cada línea por separado),
        // porque para saber si un nivel pertenece a una habilidad importa en qué línea está.
        private readonly string _textoPorLineas;

        public Contexto(string textoCv, DateOnly hoy)
        {
            Hoy = hoy;
            _textoNormalizado = TextoComparable.Normalizar(textoCv);
            _textoPorLineas = string.Join('\n', textoCv.Split('\n').Select(TextoComparable.Normalizar));
            Palabras = TextoComparable.Palabras(textoCv);
            Digitos = SoloDigitos(textoCv);
            AniosEnCv = Regex.Matches(textoCv, @"(?<!\d)(?:19|20)\d{2}(?!\d)").Select(m => m.Value).ToHashSet();
        }

        public DateOnly Hoy { get; }
        public HashSet<string> Palabras { get; }
        public string Digitos { get; }
        public HashSet<string> AniosEnCv { get; }
        public int Descartados { get; set; }

        public bool Figura(string valor) => TextoComparable.Aparece(_textoNormalizado, valor);

        /// <summary>
        /// Indica si <paramref name="cerca"/> está escrito a continuación de <paramref name="valor"/> en el mismo
        /// elemento del CV: después del nombre y antes de la siguiente coma, barra, punto y coma, barra vertical,
        /// viñeta o salto de línea. Sirve para el nivel de una habilidad o idioma: que el CV diga "avanzado" en
        /// alguna parte no prueba que ese nivel sea de Git; tiene que decirlo en el mismo elemento ("Git (avanzado)").
        /// Si el nivel está en otra línea, no se acepta (queda como dato a preguntar): es el error más seguro.
        /// </summary>
        public bool FiguraCerca(string valor, string cerca)
        {
            var buscado = TextoComparable.Normalizar(valor);
            if (buscado.Length == 0) return false;

            foreach (Match m in Regex.Matches(
                         _textoPorLineas,
                         $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(buscado)}(?![\p{{L}}\p{{N}}])"))
            {
                var desde = m.Index + m.Length;
                var hasta = Math.Min(_textoPorLineas.Length, desde + 60);
                var resto = _textoPorLineas[desde..hasta];

                var corte = resto.IndexOfAny([',', ';', '|', '/', '\n', '•']);
                if (corte >= 0) resto = resto[..corte];

                if (TextoComparable.Aparece(resto, cerca)) return true;
            }

            return false;
        }
    }
}
