using System.ComponentModel.DataAnnotations;
using Application.Cvs;
using Application.Cvs.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Usuarios.Api.Controllers;

/// <summary>
/// Gestión de los CV de la persona autenticada. La validación de pertenencia que usa el servicio
/// de postulaciones (GET api/cvs/{id}/validacion) está en <see cref="CvValidationController"/>.
/// </summary>
[ApiController]
[Authorize]
[Route("api/cvs")]
public class CvsController(CvService cvs) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        if (!TryGetUsuarioId(out var usuarioId)) return SesionInvalida();

        var lista = await cvs.ListarAsync(usuarioId, cancellationToken);
        return lista is null ? SesionInvalida() : Ok(lista);
    }

    [HttpGet("{cvId:guid}")]
    public async Task<IActionResult> Obtener(Guid cvId, CancellationToken cancellationToken)
    {
        if (!TryGetUsuarioId(out var usuarioId)) return SesionInvalida();

        var cv = await cvs.ObtenerAsync(usuarioId, cvId, cancellationToken);
        return cv is null ? CvNoEncontrado() : Ok(cv);
    }

    [HttpPost]
    public async Task<IActionResult> Crear(
        [FromBody] CrearCvRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUsuarioId(out var usuarioId)) return SesionInvalida();

        try
        {
            var cv = await cvs.CrearAsync(usuarioId, request, cancellationToken);
            return cv is null
                ? SesionInvalida()
                : CreatedAtAction(nameof(Obtener), new { cvId = cv.Id }, cv);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{cvId:guid}")]
    public async Task<IActionResult> Actualizar(
        Guid cvId, [FromBody] ActualizarCvRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUsuarioId(out var usuarioId)) return SesionInvalida();

        try
        {
            var cv = await cvs.ActualizarAsync(usuarioId, cvId, request, cancellationToken);
            return cv is null ? CvNoEncontrado() : Ok(cv);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{cvId:guid}")]
    public async Task<IActionResult> Eliminar(Guid cvId, CancellationToken cancellationToken)
    {
        if (!TryGetUsuarioId(out var usuarioId)) return SesionInvalida();

        return await cvs.EliminarAsync(usuarioId, cvId, cancellationToken)
            ? NoContent()
            : CvNoEncontrado();
    }

    // ---- Archivo del CV (RF-08) ----------------------------------------------------------------
    // El límite de la petición es un poco mayor al del archivo (el resto es el "sobre" multipart).
    // Si alguien manda algo mucho más grande, se corta antes de leerlo entero.

    /// <summary>Crea un CV a partir de un archivo (multipart/form-data: archivo, titulo?, esPrincipal?).</summary>
    [HttpPost("archivo")]
    [RequestSizeLimit(ArchivosCv.MaxBytes + 1024 * 1024)]
    public async Task<IActionResult> CrearDesdeArchivo(
        IFormFile? archivo, [FromForm] string? titulo, [FromForm] bool esPrincipal,
        CancellationToken cancellationToken)
    {
        if (!TryGetUsuarioId(out var usuarioId)) return SesionInvalida();
        if (archivo is null) return ArchivoFaltante();

        try
        {
            await using var contenido = archivo.OpenReadStream();
            var cv = await cvs.CrearDesdeArchivoAsync(
                usuarioId, new ArchivoSubido(archivo.FileName, contenido), titulo, esPrincipal, cancellationToken);

            return cv is null
                ? SesionInvalida()
                : CreatedAtAction(nameof(Obtener), new { cvId = cv.Id }, cv);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Asocia un archivo a un CV existente; si ya tenía uno, lo reemplaza.</summary>
    [HttpPut("{cvId:guid}/archivo")]
    [RequestSizeLimit(ArchivosCv.MaxBytes + 1024 * 1024)]
    public async Task<IActionResult> ReemplazarArchivo(
        Guid cvId, IFormFile? archivo, CancellationToken cancellationToken)
    {
        if (!TryGetUsuarioId(out var usuarioId)) return SesionInvalida();
        if (archivo is null) return ArchivoFaltante();

        try
        {
            await using var contenido = archivo.OpenReadStream();
            var cv = await cvs.ReemplazarArchivoAsync(
                usuarioId, cvId, new ArchivoSubido(archivo.FileName, contenido), cancellationToken);

            return cv is null ? CvNoEncontrado() : Ok(cv);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Descarga el archivo original que se subió para el CV.</summary>
    [HttpGet("{cvId:guid}/archivo")]
    public async Task<IActionResult> DescargarArchivo(Guid cvId, CancellationToken cancellationToken)
    {
        if (!TryGetUsuarioId(out var usuarioId)) return SesionInvalida();

        var archivo = await cvs.DescargarArchivoAsync(usuarioId, cvId, cancellationToken);
        if (archivo is null)
            return NotFound(new { message = "No encontramos un archivo para ese CV." });

        // El navegador no debe "adivinar" otro tipo de contenido distinto al declarado.
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(archivo.Contenido, archivo.ContentType, archivo.NombreSugerido);
    }

    /// <summary>Quita el archivo del CV; el CV y su texto se conservan.</summary>
    [HttpDelete("{cvId:guid}/archivo")]
    public async Task<IActionResult> EliminarArchivo(Guid cvId, CancellationToken cancellationToken)
    {
        if (!TryGetUsuarioId(out var usuarioId)) return SesionInvalida();

        var cv = await cvs.EliminarArchivoAsync(usuarioId, cvId, cancellationToken);
        return cv is null ? CvNoEncontrado() : Ok(cv);
    }

    private IActionResult ArchivoFaltante() =>
        BadRequest(new { message = "Adjuntá un archivo en el campo \"archivo\"." });

    private bool TryGetUsuarioId(out Guid usuarioId) =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out usuarioId);

    private IActionResult SesionInvalida() =>
        Unauthorized(new { message = "No pudimos verificar tu sesión. Iniciá sesión nuevamente." });

    // No se revela si el CV existe pero es de otra persona.
    private IActionResult CvNoEncontrado() =>
        NotFound(new { message = "No encontramos ese CV." });
}
