namespace Application.Cvs;

/// <summary>
/// Dónde se guardan físicamente los archivos de los CV. Application solo conoce esta interfaz:
/// hoy la implementación usa una carpeta del servidor, pero se puede cambiar por S3, Azure Blob, etc.
/// sin tocar las reglas de negocio.
/// </summary>
public interface IAlmacenamientoArchivos
{
    /// <summary>Guarda el contenido bajo la clave indicada (reemplaza si ya existía).</summary>
    Task GuardarAsync(string clave, Stream contenido, CancellationToken cancellationToken = default);

    /// <summary>Abre el archivo para lectura. Devuelve null si no existe. Quien lo llama debe cerrar el stream.</summary>
    Task<Stream?> AbrirAsync(string clave, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina el archivo. Es "mejor esfuerzo": no falla si el archivo no existe ni si no se pudo borrar
    /// (en ese caso la implementación lo registra), para no romper una operación que ya se guardó en la base.
    /// </summary>
    Task EliminarAsync(string clave, CancellationToken cancellationToken = default);
}
