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

    private bool TryGetUsuarioId(out Guid usuarioId) =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out usuarioId);

    private IActionResult SesionInvalida() =>
        Unauthorized(new { message = "No pudimos verificar tu sesión. Iniciá sesión nuevamente." });

    // No se revela si el CV existe pero es de otra persona.
    private IActionResult CvNoEncontrado() =>
        NotFound(new { message = "No encontramos ese CV." });
}
