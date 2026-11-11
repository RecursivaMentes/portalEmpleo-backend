using System.Text.Json;

namespace Chatbot.Infrastructure.Clients;

/// <summary>Interpreta las respuestas de error del servicio Usuarios, compartido por sus clientes.</summary>
internal static class LectorErroresUsuarios
{
    /// <summary>Extrae el mensaje de error de Usuarios ({ "message": ... } o ProblemDetails).</summary>
    public static async Task<string> LeerMensajeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var generico = $"El servicio de perfiles respondió con el estado {(int)response.StatusCode}.";

        try
        {
            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);

            if (doc.RootElement.ValueKind != JsonValueKind.Object) return generico;

            if (doc.RootElement.TryGetProperty("message", out var mensaje) && mensaje.ValueKind == JsonValueKind.String)
                return mensaje.GetString() ?? generico;

            if (doc.RootElement.TryGetProperty("errors", out var errores) && errores.ValueKind == JsonValueKind.Object)
            {
                foreach (var campo in errores.EnumerateObject())
                    foreach (var texto in campo.Value.EnumerateArray())
                        if (texto.ValueKind == JsonValueKind.String)
                            return $"{campo.Name}: {texto.GetString()}";
            }

            if (doc.RootElement.TryGetProperty("title", out var titulo) && titulo.ValueKind == JsonValueKind.String)
                return titulo.GetString() ?? generico;
        }
        catch (JsonException) { }

        return generico;
    }
}
