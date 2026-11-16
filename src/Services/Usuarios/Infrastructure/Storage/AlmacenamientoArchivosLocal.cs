using Application.Cvs;
using Microsoft.Extensions.Logging;

namespace Usuarios.Infrastructure.Storage;

/// <summary>
/// Guarda los archivos en una carpeta del servidor. La clave es una ruta relativa
/// ("{usuario}/{archivo}.pdf") dentro de <c>raiz</c>.
/// </summary>
public class AlmacenamientoArchivosLocal : IAlmacenamientoArchivos
{
    private readonly string _raiz;
    private readonly ILogger<AlmacenamientoArchivosLocal> _logger;

    public AlmacenamientoArchivosLocal(string raiz, ILogger<AlmacenamientoArchivosLocal> logger)
    {
        // Se agrega el separador final para que la comprobación de "está dentro de la raíz" sea exacta
        // (sin él, "C:\datos-otro" pasaría por estar "dentro" de "C:\datos").
        _raiz = Path.TrimEndingDirectorySeparator(Path.GetFullPath(raiz)) + Path.DirectorySeparatorChar;
        _logger = logger;
    }

    public async Task GuardarAsync(string clave, Stream contenido, CancellationToken cancellationToken = default)
    {
        var ruta = ResolverRuta(clave);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);

        // Se escribe en un archivo temporal y se renombra al final: si algo falla a mitad de camino
        // no queda un archivo cortado con el nombre definitivo.
        var temporal = ruta + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var destino = new FileStream(
                temporal, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81_920, useAsync: true))
            {
                await contenido.CopyToAsync(destino, cancellationToken);
            }

            File.Move(temporal, ruta, overwrite: true);
        }
        catch
        {
            TryBorrar(temporal);
            throw;
        }
    }

    public Task<Stream?> AbrirAsync(string clave, CancellationToken cancellationToken = default)
    {
        var ruta = ResolverRuta(clave);
        if (!File.Exists(ruta)) return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(
            ruta, FileMode.Open, FileAccess.Read, FileShare.Read, 81_920, useAsync: true);
        return Task.FromResult<Stream?>(stream);
    }

    public Task EliminarAsync(string clave, CancellationToken cancellationToken = default)
    {
        try
        {
            var ruta = ResolverRuta(clave);
            if (File.Exists(ruta)) File.Delete(ruta);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // No se propaga: el CV ya se actualizó en la base y esto solo deja un archivo huérfano.
            _logger.LogWarning(ex, "No se pudo eliminar el archivo de CV '{Clave}'.", clave);
        }

        return Task.CompletedTask;
    }

    private string ResolverRuta(string clave)
    {
        if (string.IsNullOrWhiteSpace(clave))
            throw new InvalidOperationException("La clave del archivo está vacía.");

        var ruta = Path.GetFullPath(Path.Combine(_raiz, clave));

        if (!ruta.StartsWith(_raiz, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La clave del archivo apunta fuera de la carpeta de almacenamiento.");

        return ruta;
    }

    private static void TryBorrar(string ruta)
    {
        try { if (File.Exists(ruta)) File.Delete(ruta); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
