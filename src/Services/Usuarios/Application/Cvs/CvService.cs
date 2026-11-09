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
/// </summary>
public class CvService(ICvRepository cvs, IUsuarioRepository usuarios)
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

        var existentes = await cvs.ListarPorUsuarioAsync(usuarioId, cancellationToken);
        if (existentes.Count >= MaxCvsPorUsuario)
            throw new ValidationException(
                $"Podés tener hasta {MaxCvsPorUsuario} CV. Eliminá alguno para crear otro.");

        var esPrincipal = request.EsPrincipal || existentes.Count == 0;
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
            EsPrincipal = esPrincipal,
            FechaCreacion = ahora,
            FechaActualizacion = ahora,
        };

        await cvs.AgregarAsync(cv, cancellationToken);
        await cvs.GuardarCambiosAsync(cancellationToken);

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
        return true;
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
