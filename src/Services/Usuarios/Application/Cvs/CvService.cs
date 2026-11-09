using System.ComponentModel.DataAnnotations;
using Application.Auth;
using Application.Cvs.Requests;
using Application.Cvs.Responses;
using Domain.Entities;

namespace Application.Cvs;

/// <summary>
/// Gestión de los CV de una persona. Reglas:
/// - Cada persona ve y modifica solo sus CV.
/// - Siempre hay un solo CV principal: el primero que se crea lo es, y marcar otro como principal
///   deja de serlo al anterior. Si se elimina el principal, pasa a serlo el más reciente.
/// - Hay un máximo de CV por persona.
/// - Un CV puede tener un archivo (PDF o .docx) guardado en <see cref="IAlmacenamientoArchivos"/>.
///   El archivo se escribe antes que la base y se borra después de ella, para no dejar nunca
///   un CV apuntando a un archivo que no existe (lo peor que puede pasar es un archivo huérfano).
/// </summary>
public class CvService(ICvRepository cvs, IUsuarioRepository usuarios, IAlmacenamientoArchivos archivos)
{
    public const int MaxCvsPorUsuario = 25;
    public const int MaxLargoTitulo = 200;
    public const int MaxLargoTexto = 50_000;

    public async Task<IReadOnlyList<CvResumenResponse>?> ListarAsync(
        Guid usuarioId, CancellationToken cancellationToken = default)
    {
        if (!await UsuarioActivoAsync(usuarioId, cancellationToken)) return null;

        var lista = await cvs.ListarPorUsuarioAsync(usuarioId, cancellationToken);
        return lista.Select(ToResumen).ToList();
    }

    public async Task<CvResponse?> ObtenerAsync(
        Guid usuarioId, Guid cvId, CancellationToken cancellationToken = default)
    {
        if (!await UsuarioActivoAsync(usuarioId, cancellationToken)) return null;

        var cv = await cvs.ObtenerAsync(cvId, usuarioId, cancellationToken);
        return cv is null ? null : ToResponse(cv);
    }

    public async Task<CvResponse?> CrearAsync(
        Guid usuarioId, CrearCvRequest request, CancellationToken cancellationToken = default)
    {
        if (!await UsuarioActivoAsync(usuarioId, cancellationToken)) return null;

        var titulo = ValidarTitulo(request.Titulo);
        var texto = ValidarTexto(request.TextoPlano);

        var cv = await AgregarCvAsync(usuarioId, titulo, texto, request.EsPrincipal, null, cancellationToken);
        return ToResponse(cv);
    }

    /// <summary>
    /// Crea un CV a partir de un archivo subido (RF-08). El archivo se valida y se guarda;
    /// el título es opcional (por defecto, el nombre del archivo).
    /// </summary>
    public async Task<CvResponse?> CrearDesdeArchivoAsync(
        Guid usuarioId, ArchivoSubido archivo, string? titulo, bool esPrincipal,
        CancellationToken cancellationToken = default)
    {
        if (!await UsuarioActivoAsync(usuarioId, cancellationToken)) return null;

        var tituloFinal = ValidarTitulo(
            string.IsNullOrWhiteSpace(titulo) ? ArchivosCv.TituloDesdeNombre(archivo.NombreArchivo) : titulo);
        var validado = await ArchivosCv.LeerYValidarAsync(archivo, cancellationToken);

        var clave = NuevaClaveArchivo(usuarioId, validado.Extension);
        await GuardarArchivoAsync(clave, validado, cancellationToken);

        try
        {
            var cv = await AgregarCvAsync(usuarioId, tituloFinal, null, esPrincipal, clave, cancellationToken);
            return ToResponse(cv);
        }
        catch
        {
            // Si no se pudo crear el CV, no queda un archivo huérfano.
            await archivos.EliminarAsync(clave, CancellationToken.None);
            throw;
        }
    }

    /// <summary>Asocia un archivo a un CV existente. Si ya tenía uno, lo reemplaza.</summary>
    public async Task<CvResponse?> ReemplazarArchivoAsync(
        Guid usuarioId, Guid cvId, ArchivoSubido archivo, CancellationToken cancellationToken = default)
    {
        if (!await UsuarioActivoAsync(usuarioId, cancellationToken)) return null;

        var cv = await cvs.ObtenerAsync(cvId, usuarioId, cancellationToken);
        if (cv is null) return null;

        var validado = await ArchivosCv.LeerYValidarAsync(archivo, cancellationToken);

        // El archivo nuevo va a otra clave: el anterior sigue intacto hasta que la base confirma el cambio.
        var claveNueva = NuevaClaveArchivo(usuarioId, validado.Extension);
        await GuardarArchivoAsync(claveNueva, validado, cancellationToken);

        var claveAnterior = cv.UrlArchivo;
        try
        {
            cv.UrlArchivo = claveNueva;
            cv.FechaActualizacion = DateTime.UtcNow;
            await cvs.GuardarCambiosAsync(cancellationToken);
        }
        catch
        {
            await archivos.EliminarAsync(claveNueva, CancellationToken.None);
            throw;
        }

        if (!string.IsNullOrWhiteSpace(claveAnterior))
            await archivos.EliminarAsync(claveAnterior, CancellationToken.None);

        return ToResponse(cv);
    }

    /// <summary>Abre el archivo original del CV. Devuelve null si el CV no existe, no es de la persona o no tiene archivo.</summary>
    public async Task<ArchivoDescarga?> DescargarArchivoAsync(
        Guid usuarioId, Guid cvId, CancellationToken cancellationToken = default)
    {
        if (!await UsuarioActivoAsync(usuarioId, cancellationToken)) return null;

        var cv = await cvs.ObtenerAsync(cvId, usuarioId, cancellationToken);
        if (cv is null || string.IsNullOrWhiteSpace(cv.UrlArchivo)) return null;

        var extension = Path.GetExtension(cv.UrlArchivo);
        var contentType = ArchivosCv.ContentTypePorExtension(extension);
        if (contentType is null) return null;

        var contenido = await archivos.AbrirAsync(cv.UrlArchivo, cancellationToken);
        return contenido is null
            ? null
            : new ArchivoDescarga(contenido, contentType, NombreDescarga(cv.Titulo, extension));
    }

    /// <summary>Quita el archivo del CV (el CV y su texto se conservan). Devuelve null si el CV no existe.</summary>
    public async Task<CvResponse?> EliminarArchivoAsync(
        Guid usuarioId, Guid cvId, CancellationToken cancellationToken = default)
    {
        if (!await UsuarioActivoAsync(usuarioId, cancellationToken)) return null;

        var cv = await cvs.ObtenerAsync(cvId, usuarioId, cancellationToken);
        if (cv is null) return null;

        var clave = cv.UrlArchivo;
        if (string.IsNullOrWhiteSpace(clave)) return ToResponse(cv);

        cv.UrlArchivo = null;
        cv.FechaActualizacion = DateTime.UtcNow;
        await cvs.GuardarCambiosAsync(cancellationToken);

        await archivos.EliminarAsync(clave, CancellationToken.None);
        return ToResponse(cv);
    }

    public async Task<CvResponse?> ActualizarAsync(
        Guid usuarioId, Guid cvId, ActualizarCvRequest request, CancellationToken cancellationToken = default)
    {
        if (!await UsuarioActivoAsync(usuarioId, cancellationToken)) return null;

        var cv = await cvs.ObtenerAsync(cvId, usuarioId, cancellationToken);
        if (cv is null) return null;

        if (request.Titulo is null && request.TextoPlano is null && request.EsPrincipal is null)
            throw new ValidationException(
                "Indicá al menos un dato para actualizar: el título, el contenido o si es tu CV principal.");

        // Se valida todo antes de modificar nada, para no dejar el CV a medias si algo es inválido.
        var titulo = request.Titulo is null ? null : ValidarTitulo(request.Titulo);
        var texto = request.TextoPlano is null ? null : ValidarTexto(request.TextoPlano);

        if (request.EsPrincipal is false && cv.EsPrincipal)
            throw new ValidationException(
                "Tenés que tener un CV principal. Marcá otro CV como principal y este dejará de serlo.");

        if (titulo is not null) cv.Titulo = titulo;
        if (request.TextoPlano is not null) cv.TextoPlano = texto;

        if (request.EsPrincipal is true && !cv.EsPrincipal)
        {
            var existentes = await cvs.ListarPorUsuarioAsync(usuarioId, cancellationToken);
            foreach (var otro in existentes.Where(c => c.Id != cv.Id)) otro.EsPrincipal = false;
            cv.EsPrincipal = true;
        }

        cv.FechaActualizacion = DateTime.UtcNow;
        await cvs.GuardarCambiosAsync(cancellationToken);

        return ToResponse(cv);
    }

    /// <summary>Devuelve false si el CV no existe o no es de la persona.</summary>
    public async Task<bool> EliminarAsync(
        Guid usuarioId, Guid cvId, CancellationToken cancellationToken = default)
    {
        if (!await UsuarioActivoAsync(usuarioId, cancellationToken)) return false;

        var cv = await cvs.ObtenerAsync(cvId, usuarioId, cancellationToken);
        if (cv is null) return false;

        var eraPrincipal = cv.EsPrincipal;
        cvs.Eliminar(cv);

        if (eraPrincipal)
        {
            var existentes = await cvs.ListarPorUsuarioAsync(usuarioId, cancellationToken);
            var siguiente = existentes
                .Where(c => c.Id != cv.Id)
                .OrderByDescending(c => c.FechaActualizacion)
                .FirstOrDefault();

            if (siguiente is not null) siguiente.EsPrincipal = true;
        }

        await cvs.GuardarCambiosAsync(cancellationToken);

        // Recién cuando la base confirmó el borrado se elimina el archivo físico.
        if (!string.IsNullOrWhiteSpace(cv.UrlArchivo))
            await archivos.EliminarAsync(cv.UrlArchivo, CancellationToken.None);

        return true;
    }

    /// <summary>
    /// Alta común de un CV: controla el límite, decide si es el principal (el primero siempre lo es)
    /// y deja a los demás como no principales cuando corresponde.
    /// </summary>
    private async Task<Cv> AgregarCvAsync(
        Guid usuarioId, string titulo, string? texto, bool pideSerPrincipal, string? claveArchivo,
        CancellationToken cancellationToken)
    {
        var existentes = await cvs.ListarPorUsuarioAsync(usuarioId, cancellationToken);
        if (existentes.Count >= MaxCvsPorUsuario)
            throw new ValidationException(
                $"Podés tener hasta {MaxCvsPorUsuario} CV. Eliminá alguno para crear otro.");

        var esPrincipal = pideSerPrincipal || existentes.Count == 0;
        if (esPrincipal)
        {
            foreach (var existente in existentes) existente.EsPrincipal = false;
        }

        var ahora = DateTime.UtcNow;
        var cv = new Cv
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Titulo = titulo,
            TextoPlano = texto,
            UrlArchivo = claveArchivo,
            EsPrincipal = esPrincipal,
            FechaCreacion = ahora,
            FechaActualizacion = ahora,
        };

        await cvs.AgregarAsync(cv, cancellationToken);
        await cvs.GuardarCambiosAsync(cancellationToken);
        return cv;
    }

    // La clave la genera siempre el servidor (nunca el nombre que manda el cliente), así que no hay
    // forma de escribir fuera de la carpeta de almacenamiento ni de pisar el archivo de otra persona.
    // Se guarda en Cv.UrlArchivo: es una clave de almacenamiento, no una URL pública.
    private static string NuevaClaveArchivo(Guid usuarioId, string extension) =>
        $"{usuarioId:N}/{Guid.NewGuid():N}{extension}";

    private async Task GuardarArchivoAsync(
        string clave, ArchivoValidado validado, CancellationToken cancellationToken)
    {
        using var contenido = new MemoryStream(validado.Bytes, writable: false);
        await archivos.GuardarAsync(clave, contenido, cancellationToken);
    }

    // Nombre con el que se descarga: el título del CV, sin caracteres que no valen en un nombre de archivo.
    private static string NombreDescarga(string titulo, string extension)
    {
        var invalidos = Path.GetInvalidFileNameChars();
        var limpio = new string(titulo.Select(c => invalidos.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        return (limpio.Length == 0 ? "CV" : limpio) + extension;
    }

    private async Task<bool> UsuarioActivoAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var usuario = await usuarios.GetByIdAsync(usuarioId, cancellationToken);
        return usuario is not null && usuario.Estado;
    }

    private static string ValidarTitulo(string? titulo)
    {
        var limpio = titulo?.Trim();

        if (string.IsNullOrEmpty(limpio))
            throw new ValidationException("Ingresá un título para tu CV.");

        if (limpio.Length > MaxLargoTitulo)
            throw new ValidationException($"El título no puede superar los {MaxLargoTitulo} caracteres.");

        return limpio;
    }

    /// <summary>Devuelve null si el texto está vacío.</summary>
    private static string? ValidarTexto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;

        if (texto.Length > MaxLargoTexto)
            throw new ValidationException($"El contenido del CV no puede superar los {MaxLargoTexto} caracteres.");

        return texto.Trim();
    }

    private static CvResumenResponse ToResumen(Cv cv) => new(
        cv.Id, cv.Titulo, cv.EsPrincipal, TieneArchivo(cv), cv.FechaCreacion, cv.FechaActualizacion);

    private static CvResponse ToResponse(Cv cv) => new(
        cv.Id, cv.Titulo, cv.TextoPlano, cv.EsPrincipal, TieneArchivo(cv), cv.FechaCreacion, cv.FechaActualizacion);

    private static bool TieneArchivo(Cv cv) => !string.IsNullOrWhiteSpace(cv.UrlArchivo);
}
