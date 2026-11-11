using System.Globalization;
using System.Text;

namespace Application.Cvs;

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
}
