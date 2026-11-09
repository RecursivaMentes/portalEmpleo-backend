using System.Text.RegularExpressions;
using Application.Perfil;

namespace Application.Cvs;

/// <summary>
/// Controles automáticos sobre un CV escrito por un modelo de IA, hechos con código y no con otra IA.
///
/// Un CV con datos inventados puede perjudicar a la persona, y los modelos (sobre todo los chicos) a veces
/// inventan. No se puede comprobar todo, pero sí lo más delator y fácil de verificar:
///  - que figuren todas las empresas e instituciones del perfil, escritas como están;
///  - que no aparezcan años que no están en los datos (fechas inventadas);
///  - que no se afirmen "N años" de algo si ese número no figura en los datos;
///  - que no haya emails distintos al de la persona.
/// Devuelve una lista de problemas en español (vacía = pasó los controles). Esa lista se le puede mostrar
/// al modelo para que se corrija.
/// </summary>
public static partial class VerificadorFidelidadCv
{
    public static IReadOnlyList<string> Verificar(string cv, PerfilUsuario perfil, string datos)
    {
        var problemas = new List<string>();
        var cvNormalizado = TextoComparable.Normalizar(cv);

        foreach (var empresa in Distintos(perfil.Laboral?.Experiencias.Select(e => e.Empresa)))
        {
            if (!cvNormalizado.Contains(TextoComparable.Normalizar(empresa), StringComparison.Ordinal))
                problemas.Add($"Falta la empresa \"{empresa}\" (tiene que figurar escrita exactamente así).");
        }

        foreach (var institucion in Distintos(perfil.Laboral?.Formacion.Select(f => f.Institucion)))
        {
            if (!cvNormalizado.Contains(TextoComparable.Normalizar(institucion), StringComparison.Ordinal))
                problemas.Add($"Falta la institución \"{institucion}\" (tiene que figurar escrita exactamente así).");
        }

        var aniosPermitidos = Anios(datos);
        foreach (var anio in Anios(cv).Where(a => !aniosPermitidos.Contains(a)).Order())
            problemas.Add($"El año {anio} no aparece en los datos: no inventes fechas.");

        foreach (Match m in AfirmacionDeAnios().Matches(cv))
        {
            var numero = m.Groups[1].Value;
            if (!Regex.IsMatch(datos, $@"(?<!\d){Regex.Escape(numero)}(?!\d)"))
                problemas.Add($"Dice \"{m.Value.Trim()}\" pero ese número no figura en los datos: no calcules ni inventes años de experiencia.");
        }

        foreach (Match m in Email().Matches(cv))
        {
            if (!string.Equals(m.Value, perfil.Email?.Trim(), StringComparison.OrdinalIgnoreCase))
                problemas.Add($"El email \"{m.Value}\" no es el de la persona: usá solo el que figura en los datos.");
        }

        return problemas;
    }

    private static IEnumerable<string> Distintos(IEnumerable<string>? valores) =>
        (valores ?? []).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct();

    private static HashSet<string> Anios(string texto) =>
        AnioDe4Digitos().Matches(texto).Select(m => m.Value).ToHashSet();

    [GeneratedRegex(@"\b(?:19|20)\d{2}\b")]
    private static partial Regex AnioDe4Digitos();

    // "5 años", "5+ años", "más de 3 años". Con (?<![\d/]) no se confunde con partes de una fecha.
    [GeneratedRegex(@"(?<![\d/])(\d{1,2})\s*\+?\s*a[ñn]os", RegexOptions.IgnoreCase)]
    private static partial Regex AfirmacionDeAnios();

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}")]
    private static partial Regex Email();
}
