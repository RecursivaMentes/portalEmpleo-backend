using Application.Perfil;

namespace Application.Cvs;

/// <summary>Un dato propuesto para el perfil. <see cref="YaEnPerfil"/> es true si la persona ya lo tiene cargado.</summary>
public record ItemPropuesto<T>(T Dato, bool YaEnPerfil);

/// <summary>Datos del perfil que se entendieron de un CV. Es solo una propuesta: no se guarda hasta que se confirma.</summary>
public record PropuestaPerfil(
    string? Resumen,
    string? Telefono,
    IReadOnlyList<ItemPropuesto<ExperienciaUsuario>> Experiencias,
    IReadOnlyList<ItemPropuesto<FormacionUsuario>> Formacion,
    IReadOnlyList<ItemPropuesto<HabilidadUsuario>> Habilidades,
    IReadOnlyList<ItemPropuesto<IdiomaUsuario>> Idiomas);

/// <summary>
/// Algo que el CV menciona pero que no se puede importar porque falta un dato que el perfil exige
/// (por ejemplo, una experiencia sin fecha de inicio). Se le pregunta a la persona.
/// </summary>
/// <param name="Tipo">"experiencia", "formacion" o "idioma".</param>
public record ItemIncompleto(string Tipo, string Descripcion, IReadOnlyList<string> Faltan);

/// <summary>Un dato que falta en el perfil, con la pregunta lista para hacérsela a la persona.</summary>
public record Faltante(string Campo, string Pregunta);

public record ResultadoAnalisis(
    Guid CvId,
    PropuestaPerfil Propuesta,
    IReadOnlyList<ItemIncompleto> Incompletos,
    IReadOnlyList<Faltante> Faltantes,
    IReadOnlyList<string> Avisos);
