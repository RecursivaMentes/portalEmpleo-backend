using System.Text;

namespace Application.Perfil;

/// <summary>Convierte el perfil en un texto breve que se le entrega al modelo como contexto.</summary>
public static class ContextoPerfil
{
    /// <summary>Tope de caracteres del contexto, para acotar el costo y no desbordar modelos chicos.</summary>
    private const int MaxCaracteres = 6000;

    public static string Construir(PerfilUsuario perfil)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"- Nombre: {perfil.Nombre}");

        if (TieneContenido(perfil.PreferenciasJson))
            sb.AppendLine($"- Preferencias laborales (JSON): {perfil.PreferenciasJson}");

        if (!string.IsNullOrWhiteSpace(perfil.ReglasPostulacion))
            sb.AppendLine($"- Reglas de postulación: {perfil.ReglasPostulacion}");

        if (perfil.Laboral is null)
        {
            sb.AppendLine("- Perfil laboral: todavía no cargó nada.");
        }
        else
        {
            AgregarLaboral(sb, perfil.Laboral);
        }

        var texto = sb.ToString().TrimEnd();

        return texto.Length <= MaxCaracteres
            ? texto
            : texto[..MaxCaracteres] + "\n[...perfil recortado por longitud]";
    }

    private static void AgregarLaboral(StringBuilder sb, PerfilLaboralUsuario laboral)
    {
        if (!string.IsNullOrWhiteSpace(laboral.Resumen))
            sb.AppendLine($"- Resumen: {laboral.Resumen}");

        if (laboral.SinExperiencia)
            sb.AppendLine("- Experiencia laboral: indicó que todavía no tiene experiencia.");
        else if (laboral.Experiencias.Count > 0)
        {
            sb.AppendLine("- Experiencia laboral:");
            foreach (var e in laboral.Experiencias)
            {
                var periodo = Periodo(e.FechaInicio, e.FechaFin, e.EsActual);
                var lugar = string.IsNullOrWhiteSpace(e.Ubicacion) ? "" : $", {e.Ubicacion}";
                var detalle = string.IsNullOrWhiteSpace(e.Descripcion) ? "" : $": {e.Descripcion}";
                sb.AppendLine($"  * {e.Puesto} en {e.Empresa} ({periodo}){lugar}{detalle}");
            }
        }
        else
            sb.AppendLine("- Experiencia laboral: todavía no la cargó.");

        if (laboral.Formacion.Count > 0)
        {
            sb.AppendLine("- Formación:");
            foreach (var f in laboral.Formacion)
            {
                var titulo = string.IsNullOrWhiteSpace(f.Titulo) ? f.NivelEducativo : $"{f.Titulo} ({f.NivelEducativo})";
                sb.AppendLine($"  * {titulo} en {f.Institucion} - {f.Estado}");
            }
        }
        else
            sb.AppendLine("- Formación: todavía no la cargó.");

        sb.AppendLine(laboral.Habilidades.Count > 0
            ? "- Habilidades: " + string.Join(", ", laboral.Habilidades.Select(h =>
                string.IsNullOrWhiteSpace(h.Nivel) ? h.Nombre : $"{h.Nombre} ({h.Nivel})"))
            : "- Habilidades: todavía no las cargó.");

        sb.AppendLine(laboral.Idiomas.Count > 0
            ? "- Idiomas: " + string.Join(", ", laboral.Idiomas.Select(i => $"{i.Idioma} ({i.Nivel})"))
            : "- Idiomas: todavía no los cargó.");
    }

    private static string Periodo(string? inicio, string? fin, bool esActual)
    {
        var desde = string.IsNullOrWhiteSpace(inicio) ? "?" : inicio;
        var hasta = esActual ? "actualidad" : string.IsNullOrWhiteSpace(fin) ? "?" : fin;
        return $"{desde} a {hasta}";
    }

    private static bool TieneContenido(string? json) =>
        !string.IsNullOrWhiteSpace(json) && json.Trim() is not ("{}" or "[]" or "null");
}
