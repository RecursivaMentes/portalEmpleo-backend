using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Application.Cvs;

/// <summary>Comparaciones de texto que ignoran mayúsculas, tildes y espacios repetidos.</summary>
public static class TextoComparable
{
    /// <summary>Minúsculas, sin tildes y con los espacios unificados: se compara el contenido, no la tipografía.</summary>
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;

        var sb = new StringBuilder(texto.Length);
        var ultimoFueEspacio = false;

        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;

            if (char.IsWhiteSpace(c))
            {
                if (!ultimoFueEspacio) sb.Append(' ');
                ultimoFueEspacio = true;
            }
            else
            {
                sb.Append(char.ToLowerInvariant(c));
                ultimoFueEspacio = false;
            }
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Indica si <paramref name="valor"/> figura en <paramref name="texto"/> como palabra o frase completa
    /// (así "R" no "aparece" dentro de "Rosario", ni "Java" dentro de "JavaScript"). Los dos textos se
    /// pasan por <see cref="Normalizar"/>; conviene normalizar el texto largo una sola vez y reutilizarlo.
    /// </summary>
    public static bool Aparece(string textoNormalizado, string? valor)
    {
        var buscado = Normalizar(valor);
        if (buscado.Length == 0) return false;

        return Regex.IsMatch(
            textoNormalizado,
            $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(buscado)}(?![\p{{L}}\p{{N}}])",
            RegexOptions.CultureInvariant);
    }

    /// <summary>Palabras de 4 letras o más, normalizadas. Sirven para medir cuánto de un texto proviene de otro.</summary>
    public static HashSet<string> Palabras(string? texto) =>
        Regex.Matches(Normalizar(texto), @"[\p{L}\p{N}]{4,}").Select(m => m.Value).ToHashSet();

    /// <summary>
    /// Qué fracción de las palabras de <paramref name="texto"/> están en <paramref name="palabrasDeOrigen"/>.
    /// Un texto "copiado" del origen da cerca de 1; uno inventado, cerca de 0. Si no tiene palabras, devuelve 1.
    /// </summary>
    public static double ProporcionPresente(string? texto, HashSet<string> palabrasDeOrigen)
    {
        var palabras = Palabras(texto);
        if (palabras.Count == 0) return 1;

        return (double)palabras.Count(palabrasDeOrigen.Contains) / palabras.Count;
    }
}
