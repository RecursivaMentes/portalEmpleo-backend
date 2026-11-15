using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Application.Auth;

namespace Usuarios.Api.Services;

// Filtra las modificaciones que podrían invalidar una preparación de IA activa.
// Sólo el usuario puede solicitar cancelar desde el endpoint existente de Postulaciones.
public sealed class ProteccionTrabajoIAFilter(ControlPostulacionesClient cliente,
    ILogger<ProteccionTrabajoIAFilter> logger) : IAsyncActionFilter
{
    public const string ClaveReserva = "ReservaCambioUsuarioId";

    public async Task OnActionExecutionAsync(ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var req = context.HttpContext.Request;
        var ruta = req.Path;
        var metodo = req.Method;
        var afecta = (HttpMethods.IsPost(metodo) || HttpMethods.IsPut(metodo) ||
                      HttpMethods.IsPatch(metodo) || HttpMethods.IsDelete(metodo)) &&
            (ruta.StartsWithSegments("/api/cvs") ||
             ruta.StartsWithSegments("/api/perfil-laboral") ||
             ruta.StartsWithSegments("/api/postulaciones/configuracion") ||
             string.Equals(ruta.Value, "/api/auth/profile", StringComparison.OrdinalIgnoreCase));
        // Nombre, apellido y telefono no invalidan una preparacion de IA.
        if (string.Equals(ruta.Value, "/api/auth/profile", StringComparison.OrdinalIgnoreCase) &&
            context.ActionArguments.Values.OfType<UpdateProfileRequest>().FirstOrDefault() is { } perfil &&
            perfil.PreferenciasJson is null && perfil.ReglasPostulacion is null)
            afecta = false;
        if (!afecta || !Guid.TryParse(context.HttpContext.User.FindFirst("sub")?.Value, out var usuarioId))
        {
            await next();
            return;
        }

        ResultadoReservaCambio reserva;
        try { reserva = await cliente.IniciarCambioAsync(usuarioId, context.HttpContext.RequestAborted); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "No se pudo comprobar trabajo activo para el usuario {UsuarioId}", usuarioId);
            context.Result = new ObjectResult(new { mensaje =
                "No pudimos comprobar si la IA está trabajando. Volvé a intentarlo más tarde." })
                { StatusCode = StatusCodes.Status503ServiceUnavailable };
            return;
        }
        if (!reserva.Permitido || !reserva.OperacionId.HasValue)
        {
            context.Result = new ConflictObjectResult(new
            {
                mensaje = reserva.PreparacionId.HasValue
                    ? "La IA está trabajando en una postulación. Detenela antes de modificar esta información."
                    : "Ya hay una modificación en curso. Volvé a intentarlo en unos instantes.",
                codigoError = "TRABAJO_IA_ACTIVO",
                preparacionId = reserva.PreparacionId,
                estado = reserva.Estado,
                versionEdicion = reserva.VersionEdicion,
                accionRequerida = reserva.PreparacionId.HasValue ? "CANCELAR_PREPARACION" : "REINTENTAR"
            });
            return;
        }
        var idReserva = reserva.OperacionId.Value;
        context.HttpContext.Items[ClaveReserva] = idReserva;
        try { await next(); }
        finally
        {
            try { await cliente.FinalizarCambioAsync(usuarioId, idReserva, CancellationToken.None); }
            catch (Exception ex)
            {
                // La reserva se libera por vencimiento si hay una caída de red.
                logger.LogError(ex, "No se pudo liberar una reserva de cambio del usuario {UsuarioId}", usuarioId);
            }
        }
    }
}
