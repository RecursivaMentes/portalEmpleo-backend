using System.ComponentModel.DataAnnotations;
using Application.Postulaciones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Usuarios.Api.Services;

namespace Usuarios.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/postulaciones/configuracion")]
public sealed class ConfiguracionPostulacionesController(ConfiguracionPostulacionesService configuracion,
    ControlPostulacionesClient postulaciones,
    ILogger<ConfiguracionPostulacionesController> logger)
    : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Obtener(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var usuarioId))
            return Unauthorized(new { message = "No pudimos verificar tu sesión." });

        var cuenta = await configuracion.ObtenerAsync(usuarioId, cancellationToken);
        if (cuenta is null) return NotFound(new { message = "No encontramos una cuenta activa." });
        try
        {
            var resultado = await postulaciones.ObtenerAsync(usuarioId, cancellationToken);
            return resultado is null ? NotFound(new { message = "No encontramos la configuración." }) : Ok(resultado);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return StatusCode(503, new { message = "No pudimos consultar tu configuración. Volvé a intentarlo." });
        }
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
            if (!HttpContext.Items.TryGetValue(ProteccionTrabajoIAFilter.ClaveReserva, out var reserva) ||
                reserva is not Guid operacionId)
                return Conflict(new { message = "No pudimos autorizar este cambio. Volvé a intentarlo." });
            var resultado = await configuracion.NormalizarSinGuardarAsync(usuarioId, request, cancellationToken);
            if (resultado is null) return NotFound(new { message = "No encontramos una cuenta activa." });
            var anterior = await postulaciones.ObtenerAsync(usuarioId, cancellationToken);
            await postulaciones.GuardarAsync(usuarioId, operacionId, resultado, cancellationToken);
            try
            {
                await configuracion.GuardarPreferenciasAsync(usuarioId, resultado, cancellationToken);
            }
            catch
            {
                // Compensar si Usuarios no pudo guardar sus propias preferencias.
                if (anterior is not null)
                    await postulaciones.GuardarAsync(usuarioId, operacionId, anterior, CancellationToken.None);
                throw;
            }
            return Ok(resultado);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "No se pudo coordinar configuración del usuario {UsuarioId}", usuarioId);
            return StatusCode(503, new { message = "No pudimos guardar tu configuración. Volvé a intentarlo." });
        }
    }
}
