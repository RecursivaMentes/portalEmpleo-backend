using Application.Auth;
using Application.Cvs;
using Application.Cvs.Requests;
using Application.PerfilLaboral;
using Microsoft.AspNetCore.Mvc;
using Usuarios.Api.Security;

namespace Usuarios.Api.Controllers;

[ApiController]
[Route("internal/usuarios/{usuarioId:guid}/ia")]
public sealed class ConsultasIAController(
    AuthService autenticacion, PerfilLaboralService perfiles,
    CvService cvs, IConfiguration configuracion) : ControllerBase
{
    private bool Autorizado() =>
        AutenticacionInterna.Autorizar(Request, configuracion, "Chatbot") ||
        AutenticacionInterna.Autorizar(Request, configuracion, "EmpleosYpostulaciones");

    public sealed record AplicarCvAprobadoRequest(Guid OperacionId, bool CrearNuevo, Guid? CvId,
        long? Version, string? Titulo, string Texto);

    [HttpPost("cvs/aplicar-propuesta-aprobada")]
    public async Task<IActionResult> AplicarCvAprobado(Guid usuarioId,
        [FromBody] AplicarCvAprobadoRequest request, CancellationToken ct)
    {
        if (!AutenticacionInterna.Autorizar(Request, configuracion, "EmpleosYpostulaciones"))
            return Unauthorized();
        if (usuarioId == Guid.Empty || request is null || string.IsNullOrWhiteSpace(request.Texto))
            return BadRequest();
        if (request.OperacionId == Guid.Empty) return BadRequest();
        try
        {
            var aplicado = await cvs.AplicarPropuestaAprobadaAsync(
                usuarioId, request.OperacionId, request.CrearNuevo, request.CvId,
                request.Version, request.Titulo, request.Texto, ct);
            return aplicado is null ? NotFound() : Ok(aplicado);
        }        catch (System.ComponentModel.DataAnnotations.ValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (CvConflictoException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
    [HttpGet("perfil")]
    public async Task<IActionResult> Perfil(Guid usuarioId, CancellationToken ct)
    {
        if (!Autorizado()) return Unauthorized();
        if (usuarioId == Guid.Empty) return BadRequest();
        var persona = await autenticacion.GetByIdAsync(usuarioId, ct);
        var laboral = await perfiles.ObtenerAsync(usuarioId, ct);
        return persona is null || laboral is null
            ? NotFound() : Ok(new { personales = persona, laboral });
    }

    [HttpGet("cvs")]
    public async Task<IActionResult> ListarCvs(Guid usuarioId, CancellationToken ct)
    {
        if (!Autorizado()) return Unauthorized();
        if (usuarioId == Guid.Empty) return BadRequest();
        var lista = await cvs.ListarAsync(usuarioId, ct);
        return lista is null ? NotFound() : Ok(lista);
    }

    [HttpGet("cvs/{cvId:guid}/documento")]
    public async Task<IActionResult> ObtenerDocumento(Guid usuarioId, Guid cvId, CancellationToken ct)
    {
        if (!Autorizado()) return Unauthorized();
        if (usuarioId == Guid.Empty || cvId == Guid.Empty) return BadRequest();
        // Si hay texto, el PDF generado es la version actual del CV.
        // Si no hay texto, se conserva el archivo original (PDF o Word).
        Application.Cvs.ArchivoDescarga? archivo = null;
        try { archivo = await cvs.GenerarDocumentoAsync(usuarioId, cvId, ct); }
        catch (System.ComponentModel.DataAnnotations.ValidationException)
        {
            archivo = await cvs.DescargarArchivoAsync(usuarioId, cvId, ct);
        }
        if (archivo is null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(archivo.Contenido, archivo.ContentType, archivo.NombreSugerido);
    }
    [HttpGet("cvs/{cvId:guid}")]
    public async Task<IActionResult> ObtenerCv(Guid usuarioId, Guid cvId, CancellationToken ct)
    {
        if (!Autorizado()) return Unauthorized();
        if (usuarioId == Guid.Empty || cvId == Guid.Empty) return BadRequest();
        var cv = await cvs.ObtenerAsync(usuarioId, cvId, ct);
        return cv is null ? NotFound() : Ok(cv);
    }
}