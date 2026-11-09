namespace Application.Cvs;

/// <summary>CV tal como lo devuelve el servicio Usuarios (dueño de los datos del CV).</summary>
public record CvCreado(
    Guid Id,
    string Titulo,
    string? TextoPlano,
    bool EsPrincipal,
    bool TieneArchivo,
    DateTime FechaCreacion,
    DateTime FechaActualizacion);

/// <summary>Si falló, <see cref="Mensaje"/> explica el motivo (casi siempre la validación de Usuarios).</summary>
public record ResultadoCrearCv(CvCreado? Cv, string? Mensaje = null)
{
    public bool Exito => Cv is not null;
}

public interface ICvUsuarioClient
{
    /// <summary>
    /// Crea un CV para la persona dueña del token. Usuarios aplica sus reglas (título, largo, límite de CV,
    /// primer CV = principal) y, si las rechaza, devuelve su mensaje. No lanza excepciones por fallas de red.
    /// </summary>
    Task<ResultadoCrearCv> CrearAsync(
        string accessToken, string titulo, string textoPlano, bool esPrincipal, CancellationToken cancellationToken);
}
