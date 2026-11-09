namespace Application.Cvs;

/// <summary>Genera el documento descargable de un CV (hoy PDF) a partir de sus bloques.</summary>
public interface IGeneradorDocumentoCv
{
    string ContentType { get; }

    /// <summary>Extensión con punto, por ejemplo ".pdf".</summary>
    string Extension { get; }

    byte[] Generar(IReadOnlyList<BloqueCv> bloques);
}
