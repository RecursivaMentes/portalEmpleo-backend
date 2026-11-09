namespace Application.Cvs;

/// <summary>CV tal como lo devuelve el servicio Usuarios (dueño de los datos del CV).</summary>
public record CvUsuario(
    Guid Id,
    string Titulo,
    string? TextoPlano,
    bool EsPrincipal,
    bool TieneArchivo,
    DateTime FechaCreacion,
    DateTime FechaActualizacion);

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

public interface ICvUsuarioClient
{
    /// <summary>
    /// Lee un CV de la persona dueña del token (con su texto). Usuarios responde "no encontrado" tanto si el CV
    /// no existe como si es de otra persona, y eso se traslada tal cual.
    /// </summary>
    Task<ResultadoObtenerCv> ObtenerAsync(string accessToken, Guid cvId, CancellationToken cancellationToken);

    /// <summary>
    /// Crea un CV para la persona dueña del token. Usuarios aplica sus reglas (título, largo, límite de CV,
    /// primer CV = principal) y, si las rechaza, devuelve su mensaje. No lanza excepciones por fallas de red.
    /// </summary>
    Task<ResultadoCrearCv> CrearAsync(
        string accessToken, string titulo, string textoPlano, bool esPrincipal, CancellationToken cancellationToken);
}
