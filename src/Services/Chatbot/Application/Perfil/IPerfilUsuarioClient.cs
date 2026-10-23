namespace Application.Perfil;

public interface IPerfilUsuarioClient
{
    /// <summary>
    /// Consulta el perfil de la persona dueña del token en el servicio Usuarios.
    /// Devuelve null si el servicio no está disponible o rechaza la consulta: el chat
    /// debe seguir funcionando sin el perfil, así que no se lanzan excepciones.
    /// </summary>
    Task<PerfilUsuario?> ObtenerAsync(string accessToken, CancellationToken cancellationToken);
}
