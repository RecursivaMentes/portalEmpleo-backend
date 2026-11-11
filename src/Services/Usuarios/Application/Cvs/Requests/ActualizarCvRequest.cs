namespace Application.Cvs.Requests;

/// <summary>
/// Actualización parcial: lo que se envía en null no se modifica.
/// Un <paramref name="TextoPlano"/> vacío borra el contenido del CV.
/// </summary>
public record ActualizarCvRequest(string? Titulo, string? TextoPlano, bool? EsPrincipal, long Version);
