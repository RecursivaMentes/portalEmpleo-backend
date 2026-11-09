using Application.Perfil;

namespace Application.Cvs;

public record DatosAImportar(
    string? Resumen,
    string? Telefono,
    IReadOnlyList<ExperienciaUsuario> Experiencias,
    IReadOnlyList<FormacionUsuario> Formacion,
    IReadOnlyList<HabilidadUsuario> Habilidades,
    IReadOnlyList<IdiomaUsuario> Idiomas);

public record Conteos(int Experiencias, int Formacion, int Habilidades, int Idiomas)
{
    public int Total => Experiencias + Formacion + Habilidades + Idiomas;
}

/// <param name="TelefonoNuevo">El teléfono a guardar, o null si no hay nada que guardar (ya tenía uno o no se propuso).</param>
public record ResultadoFusion(
    PerfilLaboralUsuario Perfil,
    Conteos Agregados,
    int OmitidosPorDuplicado,
    bool ResumenAplicado,
    string? TelefonoNuevo);

/// <summary>
/// Combina lo importado de un CV con el perfil que la persona ya tiene. Reglas:
///  - nunca se borra ni se modifica nada de lo existente: solo se AGREGA lo que todavía no está;
///  - dos datos son "el mismo" si coinciden los campos que los identifican (ver las claves de abajo);
///  - el resumen y el teléfono solo se completan si estaban vacíos, para no pisar lo que la persona escribió.
/// Usuarios reemplaza el perfil laboral entero al guardar, por eso el resultado es el perfil COMPLETO.
/// </summary>
public static class FusionPerfil
{
    public static string Clave(ExperienciaUsuario e) =>
        $"{TextoComparable.Normalizar(e.Empresa)}|{TextoComparable.Normalizar(e.Puesto)}|{Mes(e.FechaInicio)}";

    public static string Clave(FormacionUsuario f) =>
        $"{TextoComparable.Normalizar(f.Institucion)}|{TextoComparable.Normalizar(f.Titulo ?? f.NivelEducativo)}";

    public static string Clave(HabilidadUsuario h) => TextoComparable.Normalizar(h.Nombre);

    public static string Clave(IdiomaUsuario i) => TextoComparable.Normalizar(i.Idioma);

    /// <summary>Marca cada dato propuesto según la persona ya lo tenga o no en su perfil.</summary>
    public static PropuestaPerfil Marcar(PropuestaCruda cruda, PerfilUsuario perfil)
    {
        var l = perfil.Laboral;
        var experiencias = (l?.Experiencias ?? []).Select(Clave).ToHashSet();
        var formacion = (l?.Formacion ?? []).Select(Clave).ToHashSet();
        var habilidades = (l?.Habilidades ?? []).Select(Clave).ToHashSet();
        var idiomas = (l?.Idiomas ?? []).Select(Clave).ToHashSet();

        return new PropuestaPerfil(
            cruda.Resumen,
            cruda.Telefono,
            cruda.Experiencias.Select(e => new ItemPropuesto<ExperienciaUsuario>(e, experiencias.Contains(Clave(e)))).ToList(),
            cruda.Formacion.Select(f => new ItemPropuesto<FormacionUsuario>(f, formacion.Contains(Clave(f)))).ToList(),
            cruda.Habilidades.Select(h => new ItemPropuesto<HabilidadUsuario>(h, habilidades.Contains(Clave(h)))).ToList(),
            cruda.Idiomas.Select(i => new ItemPropuesto<IdiomaUsuario>(i, idiomas.Contains(Clave(i)))).ToList());
    }

    /// <summary>Lo que se agregaría si se aplicaran todos los datos nuevos de la propuesta.</summary>
    public static DatosAImportar SoloNuevos(PropuestaPerfil p) => new(
        p.Resumen,
        p.Telefono,
        p.Experiencias.Where(x => !x.YaEnPerfil).Select(x => x.Dato).ToList(),
        p.Formacion.Where(x => !x.YaEnPerfil).Select(x => x.Dato).ToList(),
        p.Habilidades.Where(x => !x.YaEnPerfil).Select(x => x.Dato).ToList(),
        p.Idiomas.Where(x => !x.YaEnPerfil).Select(x => x.Dato).ToList());

    public static ResultadoFusion Fusionar(PerfilUsuario perfil, DatosAImportar datos)
    {
        var actual = perfil.Laboral;

        var experiencias = (actual?.Experiencias ?? []).ToList();
        var formacion = (actual?.Formacion ?? []).ToList();
        var habilidades = (actual?.Habilidades ?? []).ToList();
        var idiomas = (actual?.Idiomas ?? []).ToList();

        var omitidos = 0;
        var agregadas = (Exp: 0, For: 0, Hab: 0, Idi: 0);

        var clavesExp = experiencias.Select(Clave).ToHashSet();
        foreach (var e in datos.Experiencias.Where(x => x is not null))
        {
            if (!Hay(e.Empresa) || !Hay(e.Puesto)) continue;
            if (!clavesExp.Add(Clave(e))) { omitidos++; continue; }
            experiencias.Add(e);
            agregadas.Exp++;
        }

        var clavesFor = formacion.Select(Clave).ToHashSet();
        foreach (var f in datos.Formacion.Where(x => x is not null))
        {
            if (!Hay(f.Institucion)) continue;
            if (!clavesFor.Add(Clave(f))) { omitidos++; continue; }
            formacion.Add(f);
            agregadas.For++;
        }

        var clavesHab = habilidades.Select(Clave).ToHashSet();
        foreach (var h in datos.Habilidades.Where(x => x is not null))
        {
            if (!Hay(h.Nombre)) continue;
            if (!clavesHab.Add(Clave(h))) { omitidos++; continue; }
            habilidades.Add(h);
            agregadas.Hab++;
        }

        var clavesIdi = idiomas.Select(Clave).ToHashSet();
        foreach (var i in datos.Idiomas.Where(x => x is not null))
        {
            if (!Hay(i.Idioma)) continue;
            if (!clavesIdi.Add(Clave(i))) { omitidos++; continue; }
            idiomas.Add(i);
            agregadas.Idi++;
        }

        // Si la persona había dicho "no tengo experiencia" y ahora se le agregan experiencias, deja de ser cierto.
        var sinExperiencia = (actual?.SinExperiencia ?? false) && agregadas.Exp == 0;

        var resumen = actual?.Resumen;
        var resumenAplicado = false;
        if (!Hay(resumen) && Hay(datos.Resumen))
        {
            resumen = datos.Resumen!.Trim();
            resumenAplicado = true;
        }

        var telefonoNuevo = !Hay(perfil.Telefono) && Hay(datos.Telefono) ? datos.Telefono!.Trim() : null;

        return new ResultadoFusion(
            new PerfilLaboralUsuario(resumen, sinExperiencia, experiencias, formacion, habilidades, idiomas),
            new Conteos(agregadas.Exp, agregadas.For, agregadas.Hab, agregadas.Idi),
            omitidos,
            resumenAplicado,
            telefonoNuevo);
    }

    private static bool Hay(string? texto) => !string.IsNullOrWhiteSpace(texto);

    // "2020-03-15" y "2020-03-01" son el mismo mes de inicio: para decidir si es la misma experiencia alcanza con año y mes.
    private static string Mes(string? fecha) =>
        string.IsNullOrWhiteSpace(fecha) ? "" : fecha.Trim()[..Math.Min(7, fecha.Trim().Length)];
}

/// <summary>Detecta qué datos le faltan al perfil y arma, con texto fijo, la pregunta para cada uno (RF-10).</summary>
public static class DetectorFaltantes
{
    public const int MaxPreguntas = 8;
    private const int MaxExperienciasSinDescripcion = 2;

    public static IReadOnlyList<Faltante> Detectar(
        PerfilLaboralUsuario? laboral, string? telefono, IReadOnlyList<ItemIncompleto> incompletos)
    {
        var faltantes = new List<Faltante>();

        // 1. Lo que el CV menciona pero no se pudo importar: es lo más concreto y lo más útil de preguntar.
        foreach (var i in incompletos)
            faltantes.Add(new Faltante($"incompleto:{i.Tipo}", PreguntaIncompleto(i)));

        // 2. Huecos del perfil, de más a menos importante.
        if (laboral is null || (!laboral.SinExperiencia && laboral.Experiencias.Count == 0))
            faltantes.Add(new Faltante("experiencia",
                "¿Tenés experiencia laboral? Si la tenés, contame dónde trabajaste, tu puesto y desde cuándo. " +
                "Si todavía no, decímelo y lo dejo anotado."));

        foreach (var e in (laboral?.Experiencias ?? [])
                     .Where(e => string.IsNullOrWhiteSpace(e.Descripcion))
                     .OrderByDescending(e => e.EsActual).ThenByDescending(e => e.FechaInicio)
                     .Take(MaxExperienciasSinDescripcion))
            faltantes.Add(new Faltante("descripcion_experiencia",
                $"En «{e.Puesto} en {e.Empresa}», ¿qué tareas o logros tuviste?"));

        if (laboral is null || laboral.Formacion.Count == 0)
            faltantes.Add(new Faltante("formacion",
                "¿Qué estudios tenés? Contame la institución, el título y si lo terminaste o lo estás cursando."));

        if (laboral is null || laboral.Habilidades.Count == 0)
            faltantes.Add(new Faltante("habilidades",
                "¿Cuáles son tus principales habilidades (tecnologías, herramientas, aptitudes)?"));

        if (laboral is null || laboral.Idiomas.Count == 0)
            faltantes.Add(new Faltante("idiomas",
                "¿Hablás algún idioma además del español? ¿Con qué nivel?"));

        if (laboral is null || string.IsNullOrWhiteSpace(laboral.Resumen))
            faltantes.Add(new Faltante("resumen",
                "Si querés, contame en dos o tres oraciones cómo te presentarías profesionalmente y lo dejo como tu resumen."));

        if (string.IsNullOrWhiteSpace(telefono))
            faltantes.Add(new Faltante("telefono", "¿Querés agregar un teléfono de contacto?"));

        return faltantes.Take(MaxPreguntas).ToList();
    }

    private static string PreguntaIncompleto(ItemIncompleto i) => i.Tipo switch
    {
        "experiencia" =>
            $"En tu CV figura «{i.Descripcion}», pero no encontré cuándo empezaste. ¿En qué mes y año fue?",
        "idioma" =>
            $"En tu CV aparece el idioma {i.Descripcion}, pero no dice tu nivel. ¿Cuál es? (de A1 a C2, o nativo)",
        _ =>
            $"De «{i.Descripcion}» me falta: {string.Join(", ", i.Faltan)}. ¿Me lo podés indicar?",
    };
}
