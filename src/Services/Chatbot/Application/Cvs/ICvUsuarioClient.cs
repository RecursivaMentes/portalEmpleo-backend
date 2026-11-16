namespace Application.Cvs;

/// <summary>CV tal como lo devuelve el servicio Usuarios (dueño de los datos del CV).</summary>
public record CvUsuario(
    Guid Id,
    string Titulo,
    string? TextoPlano,
    bool EsPrincipal,
    bool TieneArchivo,
    DateTime FechaCreacion,
    DateTime FechaActualizacion,
    long Version);

/// <summary>Si falló, <see cref="Mensaje"/> explica el motivo (casi siempre la validación de Usuarios).</summary>
public record ResultadoCrearCv(CvUsuario? Cv, string? Mensaje = null)
{
    public bool Exito => Cv is not null;
}

/// <summary>Resultado de leer un CV. Si no se pudo, <see cref="NoEncontrado"/> distingue "no existe" de "no se pudo consultar".</summary>
public record ResultadoObtenerCv(CvUsuario? Cv, bool NoEncontrado = false, string? Mensaje = null)
{
    public bool Exito => Cv is not null;
}

/// <summary>Resultado de listar los CV (sin su texto). Si falló, <see cref="Mensaje"/> explica el motivo.</summary>
public record ResultadoListarCvs(IReadOnlyList<CvUsuario>? Cvs, string? Mensaje = null)
{
    public bool Exito => Cvs is not null;
}

public interface ICvUsuarioClient
{
    /// <summary>Lista los CV de la persona dueña del token, sin su texto.</summary>
    Task<ResultadoListarCvs> ListarAsync(string accessToken, CancellationToken cancellationToken);

    /// <summary>Reemplaza el texto de un CV. Usuarios aplica sus reglas (largo máximo) y, si las rechaza, devuelve su mensaje.</summary>
    Task<ResultadoObtenerCv> ActualizarTextoAsync(
        string accessToken, Guid cvId, string textoPlano, long version, CancellationToken cancellationToken);

    /// <summary>Lee un CV de la persona dueña del token. "No encontrado" vale tanto si no existe como si es de otra persona.</summary>
    Task<ResultadoObtenerCv> ObtenerAsync(string accessToken, Guid cvId, CancellationToken cancellationToken);

    /// <summary>Crea un CV para la persona dueña del token. Usuarios aplica sus reglas y, si las rechaza, devuelve su mensaje.</summary>
    Task<ResultadoCrearCv> CrearAsync(
        string accessToken, string titulo, string textoPlano, bool esPrincipal, CancellationToken cancellationToken);
}
