using Application.Cvs;
using Application.PerfilLaboral;
using Application.Postulaciones;
using Microsoft.AspNetCore.Mvc;
using Usuarios.Api.Security;

namespace Usuarios.Api.Controllers;

// Sin JWT de usuario: exige credencial interna independiente y usuarioId explicito.
[ApiController]
[Route("internal/usuarios/{usuarioId:guid}")]
public sealed class ConsultasInternasPostulacionesController(
    PerfilLaboralService perfiles, ConfiguracionPostulacionesService configuracion,
    ICvRepository cvs, IConfiguration opciones) : ControllerBase
{
    private bool EsServicioAutorizado() =>
        AutenticacionInterna.Autorizar(Request, opciones, "EmpleosYpostulaciones") ||
        AutenticacionInterna.Autorizar(Request, opciones, "Recomendaciones");

    [HttpGet("perfil-laboral")]
    public async Task<IActionResult> ObtenerPerfil(Guid usuarioId, CancellationToken ct)
    {
        if (!EsServicioAutorizado()) return Unauthorized();
        if (usuarioId == Guid.Empty) return BadRequest();
        var perfil = await perfiles.ObtenerAsync(usuarioId, ct);
        return perfil is null ? NotFound() : Ok(perfil);
    }

    [HttpGet("perfil-laboral/validacion")]
    public async Task<IActionResult> ValidarPerfil(Guid usuarioId, CancellationToken ct)
    {
        if (!AutenticacionInterna.Autorizar(Request, opciones, "EmpleosYpostulaciones"))
            return Unauthorized();
        if (usuarioId == Guid.Empty) return BadRequest();
        var resultado = await perfiles.ValidarCompletitudAsync(usuarioId, ct);
        return resultado is null ? NotFound() : Ok(resultado);
    }

    [HttpGet("postulaciones/configuracion")]
    public async Task<IActionResult> ObtenerConfiguracion(Guid usuarioId, CancellationToken ct)
    {
        if (!AutenticacionInterna.Autorizar(Request, opciones, "EmpleosYpostulaciones"))
            return Unauthorized();
        if (usuarioId == Guid.Empty) return BadRequest();
        var resultado = await configuracion.ObtenerAsync(usuarioId, ct);
        return resultado is null ? NotFound() : Ok(resultado);
    }

    [HttpGet("cvs/{cvId:guid}/validacion")]
    public async Task<IActionResult> ValidarCv(Guid usuarioId, Guid cvId, CancellationToken ct)
    {
        if (!AutenticacionInterna.Autorizar(Request, opciones, "EmpleosYpostulaciones"))
            return Unauthorized();
        if (usuarioId == Guid.Empty || cvId == Guid.Empty) return BadRequest();
        // Una cuenta inactiva nunca autoriza CV para postularse.
        if (await perfiles.ValidarCompletitudAsync(usuarioId, ct) is null) return NotFound();
        return await cvs.PerteneceAUsuarioAsync(cvId, usuarioId, ct) ? NoContent() : NotFound();
    }
}
