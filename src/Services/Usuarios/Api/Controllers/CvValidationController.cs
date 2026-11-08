using Application.Cvs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Usuarios.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/cvs")]
public sealed class CvValidationController(ICvRepository cvs) : ControllerBase
{
    [HttpGet("{cvId:guid}/validacion")]
    public async Task<IActionResult> Validar(Guid cvId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var usuarioId))
            return Unauthorized(new { message = "Token invalido." });

        // No se revela si el CV existe pero pertenece a otro usuario.
        return await cvs.PerteneceAUsuarioAsync(cvId, usuarioId, cancellationToken)
            ? NoContent()
            : NotFound(new { message = "El CV no existe o no pertenece al usuario." });
    }
}