namespace Application.Perfil;

/// <summary>
/// Vista de solo lectura del perfil de una persona, tal como lo expone el servicio Usuarios.
/// No incluye email ni teléfono: el asistente no los necesita para conversar.
/// </summary>
public record PerfilUsuario(
    string Nombre,
    string PreferenciasJson,
    string? ReglasPostulacion,
    PerfilLaboralUsuario? Laboral);

public record PerfilLaboralUsuario(
    string? Resumen,
    bool SinExperiencia,
    IReadOnlyList<ExperienciaUsuario> Experiencias,
    IReadOnlyList<FormacionUsuario> Formacion,
    IReadOnlyList<HabilidadUsuario> Habilidades,
    IReadOnlyList<IdiomaUsuario> Idiomas);

public record ExperienciaUsuario(
    string Empresa,
    string Puesto,
    string? Ubicacion,
    string? Descripcion,
    string? FechaInicio,
    string? FechaFin,
    bool EsActual);

public record FormacionUsuario(
    string Institucion,
    string? Titulo,
    string NivelEducativo,
    string Estado,
    string? FechaInicio,
    string? FechaFin);

public record HabilidadUsuario(string Nombre, string? Nivel);

public record IdiomaUsuario(string Idioma, string Nivel);
