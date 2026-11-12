using Domain.Entities;
using Domain.Enums;

namespace Application.Postulaciones;

public class PostulacionService(IPostulacionRepository repository, ICvValidationClient cvValidation, IPerfilLaboralValidationClient perfilValidation)
{
    public async Task<Postulacion> CrearAsync(
        CrearPostulacionRequest request,
        string token,
        CancellationToken cancellationToken = default)
    {
        if (request.UsuarioId == Guid.Empty ||
            request.EmpleoId == Guid.Empty ||
            request.CvId == Guid.Empty)
        {
            throw PostulacionException.DatosInvalidos();
        }

        if (request.TextoPresentacion is { Length: > 4000 })
            throw PostulacionException.PresentacionInvalida();

        var empleo = await repository.ObtenerEmpleoAsync(
            request.EmpleoId, cancellationToken);

        if (empleo is null)
            throw PostulacionException.OfertaNoEncontrada();

        if (empleo.Estado != EstadoEmpleo.Publicado)
            throw PostulacionException.OfertaNoDisponible();

        if (empleo.FechaCierre.HasValue &&
            empleo.FechaCierre.Value <= DateTime.UtcNow)
        {
            throw PostulacionException.OfertaFinalizada();
        }

        var validacionPerfil = await perfilValidation.ValidarAsync(token, cancellationToken);
        if (!validacionPerfil.PerfilCompleto)
            throw PostulacionException.PerfilIncompleto(validacionPerfil.Faltantes);
        if (!await cvValidation.PerteneceAlUsuarioAsync(request.CvId, token, cancellationToken))
            throw PostulacionException.CvNoEncontrado();

        bool yaPostulado = await repository.ExistePostulacionAsync(
            request.UsuarioId,
            request.EmpleoId,
            cancellationToken);

        if (yaPostulado)
            throw PostulacionException.Duplicada();

        var postulacion = new Postulacion
        {
            Id = Guid.NewGuid(),
            UsuarioId = request.UsuarioId,
            EmpleoId = request.EmpleoId,
            CvId = request.CvId,
            TextoPresentacion = request.TextoPresentacion,
            Estado = EstadoPostulacion.Pendiente,
            FechaPostulacion = DateTime.UtcNow
        };

        await repository.AgregarAsync(postulacion, cancellationToken);

        return postulacion;
    }
    
    public async Task<List<PostulacionResponse>> ObtenerPorUsuarioAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new ArgumentException(
                "El identificador del usuario es obligatorio.");
        }

        var postulaciones = await repository.ObtenerPorUsuarioAsync(
            usuarioId, cancellationToken);
        return postulaciones.Select(PostulacionResponse.DesdeEntidad).ToList();
    }

    public async Task<PostulacionResponse?> ObtenerPorIdAsync(
        Guid postulacionId,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (postulacionId == Guid.Empty || usuarioId == Guid.Empty)
            throw new ArgumentException("Los identificadores de postulación y usuario son obligatorios.");

        var postulacion = await repository.ObtenerPorIdAsync(postulacionId, cancellationToken);
        if (postulacion is null || postulacion.UsuarioId != usuarioId)
            return null;

        return PostulacionResponse.DesdeEntidad(postulacion);
    }
}
