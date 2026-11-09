namespace Application.Cvs;

/// <summary>
/// Genera un documento descargable (PDF, Word...) a partir de los bloques de un CV.
/// Hay una implementación por formato; <see cref="CvService"/> elige la que corresponde.
/// Agregar un formato nuevo es escribir otra clase que implemente esta interfaz y registrarla.
/// </summary>
public interface IGeneradorDocumentoCv
{
    FormatoDocumentoCv Formato { get; }

    string ContentType { get; }

    /// <summary>Extensión con punto, por ejemplo ".pdf".</summary>
    string Extension { get; }

    byte[] Generar(IReadOnlyList<BloqueCv> bloques);
}
