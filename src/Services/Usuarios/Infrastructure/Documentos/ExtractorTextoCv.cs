using System.IO.Compression;
using System.Text;
using System.Xml;
using Application.Cvs;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Usuarios.Infrastructure.Documentos;

/// <summary>
/// Extrae el texto de CV en PDF (con PdfPig) y en Word .docx (leyendo su XML directamente).
/// </summary>
public class ExtractorTextoCv(ILogger<ExtractorTextoCv> logger) : IExtractorTextoCv
{
    // Un CV tiene pocas páginas. Se leen como mucho 30 para que un PDF enorme o malicioso
    // no ocupe el servidor, y se corta apenas hay más texto del que se puede guardar.
    private const int MaxPaginasPdf = 30;

    // Tope de lo que se descomprime de cada parte del .docx (protección contra "zip bombs":
    // archivos diminutos que al descomprimirse ocupan gigas).
    private const int MaxBytesDescomprimidosPorParte = 10 * 1024 * 1024;

    private const string NsWord = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string NsCompatibilidad = "http://schemas.openxmlformats.org/markup-compatibility/2006";

    public string Extraer(byte[] contenido, string extension)
    {
        try
        {
            return extension.ToLowerInvariant() switch
            {
                ".pdf" => ExtraerDePdf(contenido),
                ".docx" => ExtraerDeDocx(contenido),
                _ => string.Empty,
            };
        }
        catch (Exception ex)
        {
            // Se atrapa cualquier excepción a propósito: las librerías de lectura de documentos pueden
            // fallar de muchas formas con archivos dañados o hechos a medida, y en todos los casos
            // lo correcto es lo mismo: tratar el archivo como "sin texto legible", no romper la subida.
            // No es un fallo del servidor sino un archivo que no se puede leer: una línea de aviso alcanza
            // y el detalle completo queda en nivel Debug.
            logger.LogWarning("No se pudo extraer el texto de un archivo {Extension}: {Motivo}", extension, ex.Message);
            logger.LogDebug(ex, "Detalle del error al extraer texto de un archivo {Extension}.", extension);
            return string.Empty;
        }
    }

    private static string ExtraerDePdf(byte[] contenido)
    {
        var sb = new StringBuilder();

        // Si el PDF tiene contraseña de apertura, PdfPig lanza una excepción: se trata como ilegible.
        using var documento = PdfDocument.Open(contenido);

        foreach (var pagina in documento.GetPages())
        {
            if (pagina.Number > MaxPaginasPdf || sb.Length > CvService.MaxLargoTexto) break;

            // Ordena las palabras en el orden en que se leen (de arriba abajo, por columnas),
            // en vez del orden interno del archivo, que a veces es caótico.
            sb.AppendLine(ContentOrderTextExtractor.GetText(pagina));
        }

        return sb.ToString();
    }

    private static string ExtraerDeDocx(byte[] contenido)
    {
        using var zip = new ZipArchive(new MemoryStream(contenido), ZipArchiveMode.Read);
        var texto = new StringBuilder();

        // Muchos CV ponen el nombre y los datos de contacto en el encabezado de la página:
        // viven en partes aparte (word/header1.xml...), así que se leen primero.
        // Los encabezados "primera página / par / impar" suelen repetirse: se quitan los repetidos.
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var encabezados = zip.Entries
            .Where(e => e.FullName.StartsWith("word/header", StringComparison.Ordinal)
                        && e.FullName.EndsWith(".xml", StringComparison.Ordinal))
            .OrderBy(e => e.FullName, StringComparer.Ordinal);

        foreach (var encabezado in encabezados)
        {
            var parte = TextoDeParteWord(encabezado).Trim();
            if (parte.Length > 0 && vistos.Add(parte)) texto.AppendLine(parte);
        }

        var cuerpo = zip.GetEntry("word/document.xml");
        if (cuerpo is not null) texto.AppendLine(TextoDeParteWord(cuerpo));

        return texto.ToString();
    }

    private static string TextoDeParteWord(ZipArchiveEntry entrada)
    {
        var xml = LeerConLimite(entrada);

        var configuracion = new XmlReaderSettings
        {
            // Prohibir DTD y resolutores externos evita ataques de entidades XML (XXE / "billion laughs").
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
        };

        var sb = new StringBuilder();
        using var lector = XmlReader.Create(new MemoryStream(xml), configuracion);

        lector.Read();
        while (!lector.EOF)
        {
            if (lector.NodeType == XmlNodeType.Element && lector.NamespaceURI == NsCompatibilidad
                && lector.LocalName == "Fallback")
            {
                // Los cuadros de texto se guardan dos veces (versión moderna y versión de respaldo
                // para programas viejos). Se descarta la de respaldo para no duplicar el texto.
                lector.Skip();
                continue;
            }

            if (lector.NodeType == XmlNodeType.Element && lector.NamespaceURI == NsWord)
            {
                switch (lector.LocalName)
                {
                    case "pPr":
                        // Propiedades del párrafo (incluye definiciones de tabulaciones, que no son texto).
                        lector.Skip();
                        continue;

                    case "t":
                        // Un fragmento de texto. Después de leerlo el lector ya queda en el nodo siguiente.
                        sb.Append(lector.ReadElementContentAsString());
                        continue;

                    case "tab":
                        sb.Append('\t');
                        break;

                    case "br":
                    case "cr":
                        sb.Append('\n');
                        break;
                }
            }
            else if (lector.NodeType == XmlNodeType.EndElement && lector.NamespaceURI == NsWord
                     && lector.LocalName == "p")
            {
                sb.Append('\n');   // cada párrafo termina en una línea nueva
            }

            lector.Read();
        }

        return sb.ToString();
    }

    private static byte[] LeerConLimite(ZipArchiveEntry entrada)
    {
        // No se confía en el tamaño que declara el zip: se cuenta lo que realmente sale al descomprimir.
        using var origen = entrada.Open();
        using var destino = new MemoryStream();
        var buffer = new byte[81_920];
        int leidos;

        while ((leidos = origen.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (destino.Length + leidos > MaxBytesDescomprimidosPorParte)
                throw new InvalidDataException("La parte del documento es demasiado grande al descomprimirse.");

            destino.Write(buffer, 0, leidos);
        }

        return destino.ToArray();
    }
}
