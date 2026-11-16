namespace Application.Cvs.Requests;

/// <param name="Titulo">Nombre con el que la persona identifica el CV (obligatorio).</param>
/// <param name="TextoPlano">Contenido del CV en texto (opcional).</param>
/// <param name="EsPrincipal">El primer CV de una persona es siempre el principal, aunque no se indique.</param>
public record CrearCvRequest(string? Titulo, string? TextoPlano, bool EsPrincipal = false);
