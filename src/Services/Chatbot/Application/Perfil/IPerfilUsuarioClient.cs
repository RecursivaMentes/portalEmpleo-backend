namespace Application.Perfil;

/// <summary>Resultado de una escritura en Usuarios. Si falló, <see cref="Mensaje"/> explica el motivo.</summary>
public record ResultadoEscritura(bool Exito, string? Mensaje = null);

/// <summary>Campos opcionales: los que van en null no se modifican.</summary>
public record ActualizacionDatosPersonales(
    string? Nombre = null,
    string? Apellido = null,
    string? Telefono = null,
    string? PreferenciasJson = null);

public interface IPerfilUsuarioClient
{
    /// <summary>
    /// Consulta el perfil de la persona dueña del token en el servicio Usuarios.
    /// Devuelve null si el servicio no está disponible o rechaza la consulta: el chat
    /// debe seguir funcionando sin el perfil, así que no se lanzan excepciones.
    /// </summary>
    Task<PerfilUsuario?> ObtenerAsync(string accessToken, CancellationToken cancellationToken);

    /// <summary>
    /// Guarda el perfil laboral COMPLETO (Usuarios lo reemplaza entero, no lo fusiona).
    /// Si el servicio rechaza los datos, devuelve su mensaje de validación.
    /// </summary>
    Task<ResultadoEscritura> GuardarPerfilLaboralAsync(
        string accessToken, PerfilLaboralUsuario perfil, CancellationToken cancellationToken);

    Task<ResultadoEscritura> ActualizarDatosPersonalesAsync(
        string accessToken, ActualizacionDatosPersonales cambios, CancellationToken cancellationToken);
}
