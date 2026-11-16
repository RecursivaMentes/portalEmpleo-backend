namespace Application.Cvs;

/// <summary>
/// Saca el texto de un archivo de CV. Application solo conoce esta interfaz: la implementación
/// (que usa librerías para leer PDF y Word) vive en Infrastructure.
/// </summary>
public interface IExtractorTextoCv
{
    /// <summary>
    /// Devuelve el texto del archivo, o una cadena vacía si no se pudo leer nada
    /// (por ejemplo un PDF escaneado, que es solo una imagen, o un archivo dañado).
    /// Nunca lanza por un archivo ilegible: un CV mal formado no debe romper la subida.
    /// </summary>
    /// <param name="contenido">Bytes del archivo, ya validados.</param>
    /// <param name="extension">".pdf" o ".docx".</param>
    string Extraer(byte[] contenido, string extension);
}
