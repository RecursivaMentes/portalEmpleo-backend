using System.Text;
using System.Text.RegularExpressions;

namespace Application.Cvs;

/// <summary>
/// Limpieza del texto que sale de un archivo. Los extractores devuelven texto "crudo" (con espacios
/// repetidos, saltos de línea de más, caracteres raros); acá se deja en una forma uniforme y segura
/// para guardarlo en la base y mostrárselo a la persona o a la IA.
/// </summary>
public static partial class TextoCv
{
    // "ﬁ", "ﬂ"... son ligaduras tipográficas que los PDF usan como un solo carácter: se separan en letras.
    private static readonly (string Ligadura, string Letras)[] Ligaduras =
    [
        ("ﬀ", "ff"), ("ﬁ", "fi"), ("ﬂ", "fl"), ("ﬃ", "ffi"), ("ﬄ", "ffl"),
    ];

    /// <summary>
    /// Devuelve el texto limpio y recortado a <see cref="CvService.MaxLargoTexto"/> caracteres,
    /// o null si no quedó nada (archivo sin texto).
    /// </summary>
    public static string? Normalizar(string? crudo)
    {
        if (string.IsNullOrWhiteSpace(crudo)) return null;

        // Forma "compuesta": una "é" siempre es un solo carácter, no "e" + acento suelto.
        var texto = crudo.Normalize(NormalizationForm.FormC);
        foreach (var (ligadura, letras) in Ligaduras) texto = texto.Replace(ligadura, letras);

        var sb = new StringBuilder(texto.Length);
        for (var i = 0; i < texto.Length; i++)
        {
            var c = texto[i];

            if (c == '\r')
            {
                // \r\n y \r solos pasan a \n.
                if (i + 1 < texto.Length && texto[i + 1] == '\n') i++;
                sb.Append('\n');
            }
            else if (c is '\n' or '\t') sb.Append(c);
            else if (c == ' ') sb.Append(' ');                                   // espacio "duro"
            else if (char.IsHighSurrogate(c))
            {
                // Un emoji ocupa dos "chars": se conserva solo si el par está completo.
                if (i + 1 < texto.Length && char.IsLowSurrogate(texto[i + 1]))
                {
                    sb.Append(c).Append(texto[i + 1]);
                    i++;
                }
            }
            else if (char.IsLowSurrogate(c)) { /* huérfano: se descarta */ }
            // Caracteres de control (incluido el NUL, que PostgreSQL no admite en un texto),
            // el BOM, el espacio de ancho cero y el guion blando no aportan nada.
            else if (char.IsControl(c) || c is '﻿' or '​' or '­') { }
            else sb.Append(c);
        }

        // Dentro de cada línea, los espacios y tabuladores repetidos pasan a uno solo.
        var limpio = EspaciosRepetidos().Replace(sb.ToString(), " ");
        limpio = EspaciosAlBordeDeLinea().Replace(limpio, "\n");
        // Como mucho una línea en blanco seguida.
        limpio = LineasEnBlancoRepetidas().Replace(limpio, "\n\n").Trim();

        if (limpio.Length == 0) return null;

        if (limpio.Length > CvService.MaxLargoTexto)
        {
            var corte = CvService.MaxLargoTexto;
            // No cortar un emoji a la mitad.
            if (char.IsHighSurrogate(limpio[corte - 1])) corte--;
            limpio = limpio[..corte].TrimEnd();
        }

        return limpio;
    }

    [GeneratedRegex(@"[ \t]{2,}|\t")]
    private static partial Regex EspaciosRepetidos();

    [GeneratedRegex(@"[ ]*\n[ ]*")]
    private static partial Regex EspaciosAlBordeDeLinea();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex LineasEnBlancoRepetidas();
}
