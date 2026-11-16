namespace Application.Perfil;

/// <summary>
/// Vista de solo lectura del perfil de una persona, tal como lo expone el servicio Usuarios.
/// Apellido, email y teléfono se usan para armar el CV; el contexto del chat (<see cref="ContextoPerfil"/>)
/// no los incluye, porque el asistente no los necesita para conversar.
/// </summary>
public record PerfilUsuario(
    string Nombre,
    string PreferenciasJson,
    string? ReglasPostulacion,
    PerfilLaboralUsuario? Laboral,
    string Apellido = "",
    string? Email = null,
    string? Telefono = null);

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
    string? FechaFin,
    string? Descripcion);

public record HabilidadUsuario(string Nombre, string? Nivel);

public record IdiomaUsuario(string Idioma, string Nivel);
