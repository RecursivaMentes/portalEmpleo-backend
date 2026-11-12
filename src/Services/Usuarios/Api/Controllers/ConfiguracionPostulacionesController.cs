using System.ComponentModel.DataAnnotations;
using Application.Postulaciones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Usuarios.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/postulaciones/configuracion")]
public sealed class ConfiguracionPostulacionesController(ConfiguracionPostulacionesService configuracion)
    : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Obtener(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var usuarioId))
            return Unauthorized(new { message = "No pudimos verificar tu sesión." });

        var resultado = await configuracion.ObtenerAsync(usuarioId, cancellationToken);
        return resultado is null
            ? NotFound(new { message = "No encontramos una cuenta activa." })
            : Ok(resultado);
    }

    [HttpPut("me")]
    public async Task<IActionResult> Guardar(
        [FromBody] ConfiguracionPostulaciones request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var usuarioId))
            return Unauthorized(new { message = "No pudimos verificar tu sesión." });

        try
        {
            var resultado = await configuracion.GuardarAsync(usuarioId, request, cancellationToken);
            return resultado is null
                ? NotFound(new { message = "No encontramos una cuenta activa." })
                : Ok(resultado);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
