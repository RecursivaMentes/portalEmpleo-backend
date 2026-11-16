using System.Security.Cryptography;

namespace Usuarios.Api.Security;

// Una clave diferente por servicio llamador. Nunca permite acceso usando solo un usuarioId.
public static class AutenticacionInterna
{
    public static bool Autorizar(HttpRequest request, IConfiguration configuracion, string llamador)
    {
        if (!string.Equals(request.Headers["X-Internal-Service"].ToString(), llamador,
                StringComparison.Ordinal)) return false;
        var esperado = configuracion[$"InternalServices:{llamador}:Key"];
        var recibido = request.Headers["X-Internal-Key"].ToString();
        if (string.IsNullOrWhiteSpace(esperado) || string.IsNullOrWhiteSpace(recibido))
            return false;
        try
        {
            var a = Convert.FromBase64String(esperado);
            var b = Convert.FromBase64String(recibido);
            return a.Length >= 32 && a.Length == b.Length &&
                CryptographicOperations.FixedTimeEquals(a, b);
        }
        catch (FormatException) { return false; }
    }
}
