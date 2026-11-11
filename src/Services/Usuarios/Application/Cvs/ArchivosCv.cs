using System.ComponentModel.DataAnnotations;
using System.IO.Compression;
using System.Text;

namespace Application.Cvs;

/// <summary>Archivo recibido del cliente, todavía sin validar.</summary>
public record ArchivoSubido(string NombreArchivo, Stream Contenido);

/// <summary>Archivo de un CV listo para descargar. Quien lo recibe debe cerrar <see cref="Contenido"/>.</summary>
public record ArchivoDescarga(Stream Contenido, string ContentType, string NombreSugerido);

/// <summary>Archivo ya validado: sabemos qué tipo es y que su contenido coincide con ese tipo.</summary>
public record ArchivoValidado(byte[] Bytes, string Extension, string ContentType);

/// <summary>Reglas sobre los archivos de CV que se aceptan.</summary>
public static class ArchivosCv
{
    public const int MaxBytes = 5 * 1024 * 1024;

    public const string ContentTypePdf = "application/pdf";
    public const string ContentTypeDocx =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private const string MensajeTipo = "El archivo tiene que ser un PDF o un documento de Word (.docx).";

    public static string? ContentTypePorExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => ContentTypePdf,
        ".docx" => ContentTypeDocx,
        _ => null,
    };

    /// <summary>
    /// Lee el archivo (máximo <see cref="MaxBytes"/>) y comprueba que sea realmente lo que dice ser.
    /// La extensión y el tipo que declara el cliente no son confiables, así que se mira el contenido:
    /// un PDF empieza con "%PDF-" y un .docx es un zip que contiene word/document.xml.
    /// </summary>
    public static async Task<ArchivoValidado> LeerYValidarAsync(
        ArchivoSubido archivo, CancellationToken cancellationToken = default)
    {
        var nombre = Path.GetFileName(archivo.NombreArchivo ?? string.Empty);
        var extension = Path.GetExtension(nombre).ToLowerInvariant();

        var contentType = ContentTypePorExtension(extension)
            ?? throw new ValidationException(MensajeTipo);

        var bytes = await LeerLimitadoAsync(archivo.Contenido, cancellationToken);

        if (bytes.Length == 0)
            throw new ValidationException("El archivo está vacío.");

        var esValido = extension == ".pdf" ? EsPdf(bytes) : EsDocx(bytes);
        if (!esValido)
            throw new ValidationException(
                "El archivo no parece ser un " + (extension == ".pdf" ? "PDF" : "documento de Word")
                + " válido. " + MensajeTipo);

        return new ArchivoValidado(bytes, extension, contentType);
    }

    /// <summary>Título por defecto cuando la persona no lo indica: el nombre del archivo sin extensión.</summary>
    public static string TituloDesdeNombre(string nombreArchivo)
    {
        var sinExtension = Path.GetFileNameWithoutExtension(Path.GetFileName(nombreArchivo ?? string.Empty));
        return sinExtension.Trim();
    }

    private static async Task<byte[]> LeerLimitadoAsync(Stream contenido, CancellationToken cancellationToken)
    {
        // Se lee hasta MaxBytes + 1: si llega ese byte de más, el archivo es demasiado grande.
        // No se confía en la longitud que declara el cliente.
        using var memoria = new MemoryStream();
        var buffer = new byte[81_920];
        int leidos;

        while ((leidos = await contenido.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (memoria.Length + leidos > MaxBytes)
                throw new ValidationException(
                    $"El archivo no puede pesar más de {MaxBytes / (1024 * 1024)} MB.");

            memoria.Write(buffer, 0, leidos);
        }

        return memoria.ToArray();
    }

    // La especificación permite hasta 1024 bytes de basura antes de "%PDF-".
    private static bool EsPdf(byte[] bytes)
    {
        var inicio = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 1024));
        return inicio.Contains("%PDF-", StringComparison.Ordinal);
    }

    private static bool EsDocx(byte[] bytes)
    {
        // Un zip empieza con "PK\x03\x04".
        if (bytes.Length < 4 || bytes[0] != 0x50 || bytes[1] != 0x4B || bytes[2] != 0x03 || bytes[3] != 0x04)
            return false;

        try
        {
            // Solo se lee el índice del zip, no se descomprime nada.
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            return zip.GetEntry("word/document.xml") is not null;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }
}
