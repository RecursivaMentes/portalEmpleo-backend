using Domain.Entities;
using Domain.Enums;

namespace Application.Postulaciones;

public class PostulacionService(IPostulacionRepository repository)
{
    public async Task<Postulacion> CrearAsync(
        CrearPostulacionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.UsuarioId == Guid.Empty ||
            request.EmpleoId == Guid.Empty ||
            request.CvId == Guid.Empty)
        {
            throw new ArgumentException(
                "UsuarioId, EmpleoId y CvId son obligatorios.");
        }

        var empleo = await repository.ObtenerEmpleoAsync(
            request.EmpleoId, cancellationToken);

        if (empleo is null)
            throw new KeyNotFoundException("La oferta laboral no existe.");

        if (empleo.Estado != EstadoEmpleo.Publicado)
            throw new InvalidOperationException(
                "La oferta laboral no está publicada.");

        if (empleo.FechaCierre.HasValue &&
            empleo.FechaCierre.Value <= DateTime.UtcNow)
        {
            throw new InvalidOperationException(
                "La oferta laboral ya finalizó.");
        }

        bool yaPostulado = await repository.ExistePostulacionAsync(
            request.UsuarioId,
            request.EmpleoId,
            cancellationToken);

        if (yaPostulado)
            throw new InvalidOperationException(
                "El usuario ya se postuló a esta oferta.");

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
    
    public async Task<List<Postulacion>> ObtenerPorUsuarioAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new ArgumentException(
                "El identificador del usuario es obligatorio.");
        }

        return await repository.ObtenerPorUsuarioAsync(
            usuarioId,
            cancellationToken);
    }
}
