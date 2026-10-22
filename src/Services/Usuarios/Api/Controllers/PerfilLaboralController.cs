using System.ComponentModel.DataAnnotations;
using System.Data;
using Application.PerfilLaboral;
using Application.PerfilLaboral.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Usuarios.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/perfil-laboral")]
public class PerfilLaboralController(PerfilLaboralService perfiles) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Obtener(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var usuarioId))
            return Unauthorized(new { message = "No pudimos verificar tu sesión. Iniciá sesión nuevamente." });

        var perfil = await perfiles.ObtenerAsync(usuarioId, cancellationToken);
        if (perfil is null)
            return NotFound(new { message = "Todavía no tenés un perfil laboral guardado." });

        return Ok(perfil);
    }

    [HttpPut("me")]
    public async Task<IActionResult> Guardar(
        [FromBody] GuardarPerfilLaboralRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var usuarioId))
            return Unauthorized(new { message = "No pudimos verificar tu sesión. Iniciá sesión nuevamente." });

        try
        {
            var resultado = await perfiles.GuardarAsync(usuarioId, request, cancellationToken);
            if (resultado is null)
                return NotFound(new { message = "No pudimos acceder a tu cuenta. Volvé a iniciar sesión." });

            return resultado.Value.Creado
                ? StatusCode(201, resultado.Value.Perfil)
                : Ok(resultado.Value.Perfil);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (DuplicateNameException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
