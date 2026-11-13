using System.ComponentModel.DataAnnotations;
using Application.Auth;
using Application.Cvs.Requests;
using Application.Cvs.Responses;
using Domain.Entities;

namespace Application.Cvs;

/// <summary>
/// Gestión de los CV de una persona. Cada persona ve y modifica solo los suyos; siempre hay un único CV principal
/// (el primero que se crea; si se elimina, pasa a serlo el más reciente) y hay un máximo por persona.
/// El archivo de un CV se escribe antes que la base y se borra después, para que nunca quede un CV apuntando a
/// un archivo inexistente (lo peor que puede pasar es un archivo huérfano).
/// </summary>
public class CvService(
    ICvRepository cvs,
    IUsuarioRepository usuarios,
    IAlmacenamientoArchivos archivos,
    IExtractorTextoCv extractor,
    IGeneradorDocumentoCv generador)
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

    /// <summary>Crea un CV a partir de un archivo subido (RF-08); el título es opcional (por defecto, el nombre del archivo).</summary>
    public async Task<CvResponse?> CrearDesdeArchivoAsync(
        Guid usuarioId, ArchivoSubido archivo, string? titulo, bool esPrincipal,
        CancellationToken cancellationToken = default)
    {
        if (!await UsuarioActivoAsync(usuarioId, cancellationToken)) return null;

        var tituloFinal = ValidarTitulo(
            string.IsNullOrWhiteSpace(titulo) ? ArchivosCv.TituloDesdeNombre(archivo.NombreArchivo) : titulo);
        var validado = await ArchivosCv.LeerYValidarAsync(archivo, cancellationToken);

        // Si el archivo no tiene texto legible (p. ej. un PDF escaneado), el CV se crea igual, sin texto.
        var texto = TextoCv.Normalizar(extractor.Extraer(validado.Bytes, validado.Extension));

        var clave = NuevaClaveArchivo(usuarioId, validado.Extension);
        await GuardarArchivoAsync(clave, validado, cancellationToken);

        try
        {
            var cv = await AgregarCvAsync(usuarioId, tituloFinal, texto, esPrincipal, clave, cancellationToken);
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
        Guid usuarioId, Guid cvId, ArchivoSubido archivo, long version, CancellationToken cancellationToken = default)
    {
        if (!await UsuarioActivoAsync(usuarioId, cancellationToken)) return null;

        var cv = await cvs.ObtenerAsync(cvId, usuarioId, cancellationToken);
        if (cv is null) return null;
        ComprobarVersion(cv, version);

        var validado = await ArchivosCv.LeerYValidarAsync(archivo, cancellationToken);

        // El archivo nuevo va a otra clave: el anterior sigue intacto hasta que la base confirma el cambio.
        var claveNueva = NuevaClaveArchivo(usuarioId, validado.Extension);
        await GuardarArchivoAsync(claveNueva, validado, cancellationToken);

        // El texto pasa a ser el del archivo nuevo. Si el archivo nuevo no tiene texto legible,
        // se conserva el que había: es mejor que dejar el CV vacío.
        var texto = TextoCv.Normalizar(extractor.Extraer(validado.Bytes, validado.Extension));

        var claveAnterior = cv.UrlArchivo;
        try
        {
            cv.UrlArchivo = claveNueva;
            if (texto is not null) cv.TextoPlano = texto;
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

    /// <summary>
    /// Vuelve a leer el texto del archivo guardado y lo pone como contenido del CV. Devuelve null si el CV no existe;
    /// lanza <see cref="ValidationException"/> si no hay archivo o no se pudo sacar texto de él.
    /// </summary>
    public async Task<CvResponse?> ExtraerTextoAsync(
        Guid usuarioId, Guid cvId, long version, CancellationToken cancellationToken = default)
    {
        if (!await UsuarioActivoAsync(usuarioId, cancellationToken)) return null;

        var cv = await cvs.ObtenerAsync(cvId, usuarioId, cancellationToken);
        if (cv is null) return null;
        ComprobarVersion(cv, version);

        if (string.IsNullOrWhiteSpace(cv.UrlArchivo))
            throw new ValidationException(
                "Este CV no tiene un archivo. Subí un PDF o un Word para poder leer su texto.");

        var extension = Path.GetExtension(cv.UrlArchivo);

        await using var contenido = await archivos.AbrirAsync(cv.UrlArchivo, cancellationToken)
            ?? throw new ValidationException("El archivo de este CV ya no está disponible. Volvé a subirlo.");

        var validado = await ArchivosCv.LeerYValidarAsync(
            new ArchivoSubido("cv" + extension, contenido), cancellationToken);

        var texto = TextoCv.Normalizar(extractor.Extraer(validado.Bytes, validado.Extension))
            ?? throw new ValidationException(
                "No pudimos leer texto de este archivo. Si es un PDF escaneado (una imagen), " +
                "no se puede extraer el texto: escribilo a mano o subí una versión con texto seleccionable.");

        cv.TextoPlano = texto;
        cv.FechaActualizacion = DateTime.UtcNow;
        await cvs.GuardarCambiosAsync(cancellationToken);

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

    /// <summary>Genera el PDF del CV a partir de su texto (RF-13). A diferencia del archivo original, refleja las ediciones.</summary>
    public async Task<ArchivoDescarga?> GenerarDocumentoAsync(
        Guid usuarioId, Guid cvId, CancellationToken cancellationToken = default)
    {
        if (!await UsuarioActivoAsync(usuarioId, cancellationToken)) return null;

        var cv = await cvs.ObtenerAsync(cvId, usuarioId, cancellationToken);
        if (cv is null) return null;

        // Misma limpieza que el texto extraído: también cubre caracteres colados al editar a mano.
        var texto = TextoCv.Normalizar(cv.TextoPlano)
            ?? throw new ValidationException(
                "Este CV no tiene contenido para descargar. Escribilo o subí un archivo que tenga texto.");

        var bytes = generador.Generar(EstructuraCv.Parsear(texto));

        return new ArchivoDescarga(
            new MemoryStream(bytes, writable: false),
            generador.ContentType,
            NombreDescarga(cv.Titulo, generador.Extension));
    }

    /// <summary>Quita el archivo del CV (el CV y su texto se conservan). Devuelve null si el CV no existe.</summary>
    public async Task<CvResponse?> EliminarArchivoAsync(
        Guid usuarioId, Guid cvId, long version, CancellationToken cancellationToken = default)
    {
        if (!await UsuarioActivoAsync(usuarioId, cancellationToken)) return null;

        var cv = await cvs.ObtenerAsync(cvId, usuarioId, cancellationToken);
        if (cv is null) return null;
        ComprobarVersion(cv, version);

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
        if (request.Version < 1)
            throw new ValidationException("Envia la version del CV que consultaste.");

        if (request.EsPrincipal is true)
            return await cvs.EjecutarExclusivoAsync(usuarioId,
                () => ActualizarCvCoreAsync(usuarioId, cvId, request, cancellationToken), cancellationToken);
        return await ActualizarCvCoreAsync(usuarioId, cvId, request, cancellationToken);
    }

    private async Task<CvResponse?> ActualizarCvCoreAsync(
        Guid usuarioId, Guid cvId, ActualizarCvRequest request, CancellationToken cancellationToken)
    {
        var cv = await cvs.ObtenerAsync(cvId, usuarioId, cancellationToken);
        if (cv is null) return null;
        if (cv.Version != request.Version)
            throw new CvConflictoException("El CV cambio desde que lo consultaste. Actualiza y volve a intentar.");

        if (request.Titulo is null && request.TextoPlano is null && request.EsPrincipal is null)
            throw new ValidationException(
                "Indicá al menos un dato para actualizar: el título, el contenido o si es tu CV principal.");

        var titulo = request.Titulo is null ? null : ValidarTitulo(request.Titulo);
        var texto = request.TextoPlano is null ? null : ValidarTexto(request.TextoPlano);

        if (request.EsPrincipal is false && cv.EsPrincipal)
            throw new ValidationException(
                "Tenés que tener un CV principal. Marcá otro CV como principal y este dejará de serlo.");

        if (request.EsPrincipal is true && !cv.EsPrincipal)
        {
            var existentes = await cvs.ListarPorUsuarioAsync(usuarioId, cancellationToken);
            foreach (var otro in existentes.Where(c => c.Id != cv.Id)) otro.EsPrincipal = false;
            // Primera escritura dentro de la misma transaccion, antes de marcar el nuevo principal.
            await cvs.GuardarCambiosAsync(cancellationToken);
            cv.EsPrincipal = true;
        }

        if (titulo is not null) cv.Titulo = titulo;
        if (request.TextoPlano is not null) cv.TextoPlano = texto;
        cv.FechaActualizacion = DateTime.UtcNow;
        await cvs.GuardarCambiosAsync(cancellationToken);
        return ToResponse(cv);
    }

    /// <summary>Devuelve false si el CV no existe o no es de la persona.</summary>
    public async Task<bool> EliminarAsync(
        Guid usuarioId, Guid cvId, long version, CancellationToken cancellationToken = default)
    {
        if (!await UsuarioActivoAsync(usuarioId, cancellationToken)) return false;
        if (version < 1) throw new ValidationException("Envia la version del CV que consultaste.");

        var clave = await cvs.EjecutarExclusivoAsync(usuarioId, async () =>
        {
            var cv = await cvs.ObtenerAsync(cvId, usuarioId, cancellationToken);
            if (cv is null) return (Encontrado: false, Archivo: (string?)null);
            if (cv.Version != version)
                throw new CvConflictoException("El CV cambio desde que lo consultaste. Actualiza y volve a intentar.");

            var eraPrincipal = cv.EsPrincipal;
            cvs.Eliminar(cv);
            await cvs.GuardarCambiosAsync(cancellationToken);
            if (eraPrincipal)
            {
                var existentes = await cvs.ListarPorUsuarioAsync(usuarioId, cancellationToken);
                var siguiente = existentes.OrderByDescending(c => c.FechaActualizacion).FirstOrDefault();
                if (siguiente is not null)
                {
                    siguiente.EsPrincipal = true;
                    await cvs.GuardarCambiosAsync(cancellationToken);
                }
            }
            return (Encontrado: true, Archivo: cv.UrlArchivo);
        }, cancellationToken);

        if (!clave.Encontrado) return false;
        if (!string.IsNullOrWhiteSpace(clave.Archivo))
            await archivos.EliminarAsync(clave.Archivo, CancellationToken.None);
        return true;
    }

    /// <summary>Alta común: controla el límite y decide si el CV es el principal (el primero siempre lo es).</summary>
    private Task<Cv> AgregarCvAsync(Guid usuarioId, string titulo, string? texto,
        bool pideSerPrincipal, string? claveArchivo, CancellationToken ct) =>
        cvs.EjecutarExclusivoAsync(usuarioId,
            () => AgregarCvCoreAsync(usuarioId, titulo, texto, pideSerPrincipal, claveArchivo, ct), ct);

    // Este metodo se usa tanto en altas habituales como en propuestas IA aprobadas,
    // siempre bajo la transaccion y el bloqueo por usuario de EjecutarExclusivoAsync.
    private async Task<Cv> AgregarCvCoreAsync(Guid usuarioId, string titulo, string? texto,
        bool pideSerPrincipal, string? claveArchivo, CancellationToken ct)
    {
        var existentes = await cvs.ListarPorUsuarioAsync(usuarioId, ct);
        if (existentes.Count >= MaxCvsPorUsuario)
            throw new ValidationException($"PodÃ©s tener hasta {MaxCvsPorUsuario} CV. EliminÃ¡ alguno para crear otro.");
        var esPrincipal = pideSerPrincipal || existentes.Count == 0;
        if (esPrincipal)
        {
            foreach (var existente in existentes) existente.EsPrincipal = false;
            await cvs.GuardarCambiosAsync(ct);
        }
        var ahora = DateTime.UtcNow;
        var cv = new Cv
        {
            Id = Guid.NewGuid(), UsuarioId = usuarioId, Titulo = titulo,
            TextoPlano = texto, UrlArchivo = claveArchivo, EsPrincipal = esPrincipal,
            Version = 1, FechaCreacion = ahora, FechaActualizacion = ahora,
        };
        await cvs.AgregarAsync(cv, ct);
        await cvs.GuardarCambiosAsync(ct);
        return cv;
    }
    public sealed record CvIAAplicado(Guid Id, long Version);

    public async Task<CvIAAplicado?> AplicarPropuestaAprobadaAsync(
        Guid usuarioId, Guid operacionId, bool crearNuevo, Guid? cvId,
        long? versionOriginal, string? titulo, string texto, CancellationToken ct = default)
    {
        if (usuarioId == Guid.Empty || operacionId == Guid.Empty || string.IsNullOrWhiteSpace(texto))
            throw new ValidationException("La operaciÃ³n y el texto del CV son obligatorios.");
        if (crearNuevo && cvId.HasValue || !crearNuevo && (cvId is null || versionOriginal is null or < 1))
            throw new ValidationException("La propuesta no identifica correctamente el CV original.");
        if (!await UsuarioActivoAsync(usuarioId, ct)) return null;
        var textoLimpio = ValidarTexto(texto)
            ?? throw new ValidationException("El CV no puede quedar vacÃ­o.");
        var tituloLimpio = crearNuevo ? ValidarTitulo(titulo) : null;
        var solicitud = System.Text.Json.JsonSerializer.Serialize(new {
            usuarioId, crearNuevo, cvId, versionOriginal, titulo = tituloLimpio, texto = textoLimpio
        });
        var huella = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(solicitud)));

        return await cvs.EjecutarExclusivoAsync(usuarioId, async () =>
        {
            var anterior = await cvs.ObtenerOperacionCvIAAsync(operacionId, usuarioId, ct);
            if (anterior is not null)
            {
                if (anterior.HuellaSolicitud != huella)
                    throw new CvConflictoException("La operaciÃ³n de aprobaciÃ³n ya fue utilizada con otros datos.");
                return new CvIAAplicado(anterior.CvId, anterior.VersionResultado);
            }
            CvIAAplicado resultado;
            if (crearNuevo)
            {
                var creado = await AgregarCvCoreAsync(usuarioId, tituloLimpio!, textoLimpio,
                    false, null, ct);
                resultado = new CvIAAplicado(creado.Id, creado.Version);
            }
            else
            {
                var actualizado = await ActualizarCvCoreAsync(usuarioId, cvId!.Value,
                    new ActualizarCvRequest(null, textoLimpio, null, versionOriginal!.Value), ct);
                if (actualizado is null) return null;
                resultado = new CvIAAplicado(actualizado.Id, actualizado.Version);
            }
            await cvs.RegistrarOperacionCvIAAsync(new OperacionCvIA
            {
                Id = operacionId, UsuarioId = usuarioId, CvId = resultado.Id,
                VersionResultado = resultado.Version, HuellaSolicitud = huella,
                FechaUtc = DateTime.UtcNow
            }, ct);
            await cvs.GuardarCambiosAsync(ct);
            return resultado;
        }, ct);
    }
    // La clave la genera siempre el servidor (nunca el nombre del cliente): no se puede escribir fuera de la carpeta
    // ni pisar el archivo de otra persona. Se guarda en Cv.UrlArchivo, que es una clave de almacenamiento, no una URL.
    private static void ComprobarVersion(Cv cv, long version)
    {
        if (version < 1) throw new ValidationException("Envia la version del CV que consultaste.");
        if (cv.Version != version)
            throw new CvConflictoException("El CV cambio desde que lo consultaste. Actualiza y volve a intentar.");
    }

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
        // Un título es una sola línea: se quitan también los saltos de línea y demás caracteres de control.
        var limpio = titulo is null
            ? null
            : TextoCv.QuitarCaracteresInvalidos(titulo, conservarSaltosDeLinea: false).Trim();

        if (string.IsNullOrEmpty(limpio))
            throw new ValidationException("Ingresá un título para tu CV.");

        if (limpio.Length > MaxLargoTitulo)
            throw new ValidationException($"El título no puede superar los {MaxLargoTitulo} caracteres.");

        return limpio;
    }

    /// <summary>Devuelve null si el texto está vacío.</summary>
    private static string? ValidarTexto(string? texto)
    {
        if (texto is null) return null;

        // Sin esto, un carácter NUL en el texto haría fallar el guardado en PostgreSQL con un error 500.
        // Se limpia antes de mirar si quedó vacío: un texto de solo caracteres inválidos vale como vacío.
        texto = TextoCv.QuitarCaracteresInvalidos(texto);
        if (string.IsNullOrWhiteSpace(texto)) return null;

        if (texto.Length > MaxLargoTexto)
            throw new ValidationException($"El contenido del CV no puede superar los {MaxLargoTexto} caracteres.");

        return texto.Trim();
    }

    private static CvResumenResponse ToResumen(Cv cv) => new(
        cv.Id, cv.Titulo, cv.EsPrincipal, TieneArchivo(cv), cv.FechaCreacion, cv.FechaActualizacion, cv.Version);

    private static CvResponse ToResponse(Cv cv) => new(
        cv.Id, cv.Titulo, cv.TextoPlano, cv.EsPrincipal, TieneArchivo(cv), cv.FechaCreacion, cv.FechaActualizacion, cv.Version);

    private static bool TieneArchivo(Cv cv) => !string.IsNullOrWhiteSpace(cv.UrlArchivo);
}
