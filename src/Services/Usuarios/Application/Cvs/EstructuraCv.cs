namespace Application.Cvs;

public enum TipoBloque
{
    /// <summary>Título principal (normalmente el nombre). Se escribe como "# Nombre".</summary>
    Titulo,

    /// <summary>Título de una sección ("Experiencia", "Educación"...). Se escribe como "## Sección".</summary>
    Subtitulo,

    /// <summary>Elemento de una lista. Se escribe como "- texto".</summary>
    Vineta,

    /// <summary>Una línea de texto normal.</summary>
    Linea,

    /// <summary>Separación entre grupos de líneas.</summary>
    EspacioEnBlanco,
}

public record BloqueCv(TipoBloque Tipo, string Texto);

/// <summary>
/// Convierte el texto de un CV en bloques para dibujarlo con formato. El texto usa marcas livianas:
///   # Nombre → título · ## Sección → sección · - algo → viñeta · línea vacía → espacio · otra línea → texto normal.
/// Una línea corta toda en MAYÚSCULAS ("EXPERIENCIA LABORAL") también es una sección, así el texto sacado de un
/// archivo subido (que no trae marcas) sale con un formato razonable.
/// </summary>
public static class EstructuraCv
{
    private const int MaxLargoSeccionEnMayusculas = 40;
    private const int MinLetrasSeccionEnMayusculas = 3;

    private static readonly string[] MarcasDeVineta = ["- ", "* ", "• ", "– "];

    public static IReadOnlyList<BloqueCv> Parsear(string texto)
    {
        var bloques = new List<BloqueCv>();

        foreach (var cruda in texto.Split('\n'))
        {
            var linea = cruda.Trim();

            if (linea.Length == 0)
            {
                // Varias líneas en blanco seguidas valen como una sola, y no se empieza con un espacio.
                if (bloques.Count > 0 && bloques[^1].Tipo != TipoBloque.EspacioEnBlanco)
                    bloques.Add(new BloqueCv(TipoBloque.EspacioEnBlanco, string.Empty));
                continue;
            }

            bloques.Add(Clasificar(linea));
        }

        // Un espacio al final no sirve para nada.
        if (bloques.Count > 0 && bloques[^1].Tipo == TipoBloque.EspacioEnBlanco)
            bloques.RemoveAt(bloques.Count - 1);

        return bloques;
    }

    private static BloqueCv Clasificar(string linea)
    {
        // "###" y "##" son la misma cosa: una sección. Se prueba "###" primero porque empieza igual que "##".
        if (TryQuitarMarca(linea, "### ", out var resto) || TryQuitarMarca(linea, "## ", out resto))
            return new BloqueCv(TipoBloque.Subtitulo, resto);

        if (TryQuitarMarca(linea, "# ", out resto))
            return new BloqueCv(TipoBloque.Titulo, resto);

        foreach (var marca in MarcasDeVineta)
        {
            if (TryQuitarMarca(linea, marca, out resto))
                return new BloqueCv(TipoBloque.Vineta, resto);
        }

        return EsSeccionEnMayusculas(linea)
            ? new BloqueCv(TipoBloque.Subtitulo, linea)
            : new BloqueCv(TipoBloque.Linea, linea);
    }

    // La marca solo cuenta si después hay texto: una línea que es solo "-" o "##" se deja como está.
    private static bool TryQuitarMarca(string linea, string marca, out string resto)
    {
        resto = string.Empty;
        if (!linea.StartsWith(marca, StringComparison.Ordinal)) return false;

        resto = linea[marca.Length..].Trim();
        return resto.Length > 0;
    }

    // Solo letras en mayúscula, espacios y unos pocos signos; sin números, @, comas ni puntos,
    // para no confundir con listas de tecnologías ("SQL, C#") o datos de contacto.
    private static bool EsSeccionEnMayusculas(string linea)
    {
        if (linea.Length > MaxLargoSeccionEnMayusculas) return false;

        var letras = 0;
        foreach (var c in linea)
        {
            if (char.IsLetter(c))
            {
                if (!char.IsUpper(c)) return false;
                letras++;
            }
            else if (c is not (' ' or '&' or '/' or ':' or '-')) return false;
        }

        return letras >= MinLetrasSeccionEnMayusculas;
    }
}
