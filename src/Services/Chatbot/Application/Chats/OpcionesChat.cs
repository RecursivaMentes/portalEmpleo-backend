namespace Application.Chats;

/// <param name="HerramientasPerfil">
/// Si es true, el asistente puede guardar datos en el perfil de la persona (RF-06).
/// Conviene apagarlo con modelos locales que no llamen bien a las herramientas.
/// </param>
public record OpcionesChat(bool HerramientasPerfil = true);
